// =========================================================================
// Program.cs — Ponto de entrada da Gateway API (Exchange.Gateway.Api)
// =========================================================================
//
// VISÃO GERAL:
//   Esta é a Gateway API da plataforma de trading. Ela funciona como a
//   "porta de entrada" (API REST) para todas as operações do sistema,
//   seguindo o padrão Minimal API do ASP.NET Core.
//
//   A Gateway API NÃO contém lógica de negócio diretamente — ela apenas
//   recebe requisições HTTP, delega o processamento para os serviços da
//   camada Application e devolve os resultados formatados como JSON.
//
// ARQUITETURA (Camadas referenciadas):
//
//   [Gateway API]  → Camada de apresentação (este arquivo)
//        ↓
//   [Application]  → Lógica de aplicação (serviços, orquestração)
//        ↓             Projeto: libs/dotnet/Exchange.Trading.Application
//   [Domain]       → Entidades, Value Objects e enums do domínio
//        ↓             Projeto: libs/dotnet/Exchange.Trading.Domain
//   [Infrastructure] → Repositórios, Kafka, Matching Engine
//        ↓             Projeto: libs/dotnet/Exchange.Trading.Infrastructure
//   [Contracts]    → Commands, ReadModels e DTOs compartilhados
//                      Projeto: libs/contracts/dotnet/Exchange.Platform.Contracts
//
// DEPENDÊNCIAS DO PROJETO (.csproj):
//   - Exchange.Trading.Application     (serviços de aplicação)
//   - Exchange.Trading.Infrastructure  (repositórios, Kafka, matching engine)
//   - Exchange.Platform.Contracts      (commands, read models, enums compartilhados)
// =========================================================================

// Conversor para serializar enums como strings no JSON (ex: "Buy" em vez de 0)
using System.Text.Json.Serialization;

// Commands (DTOs de entrada) — definem os dados necessários para cada operação
// Arquivo: libs/contracts/dotnet/Exchange.Platform.Contracts/Commands/
//   - CreateAccountCommand.cs  → Dados para criar conta (AccountId, DisplayName, Email)
//   - FundAccountCommand.cs    → Dados para depositar fundos (AccountId, Asset, Amount)
//   - CreateOrderCommand.cs    → Dados para criar ordem (OrderId, Symbol, Side, Type, Quantity, Price...)
//   - CancelOrderCommand.cs    → Dados para cancelar ordem (OrderId, AccountId, Symbol)
using Exchange.Platform.Contracts.Commands;

// ReadModels (DTOs de saída / consulta) — representações de leitura dos dados
// Arquivo: libs/contracts/dotnet/Exchange.Platform.Contracts/ReadModels/AccountSummary.cs
//   - AccountSummary       → Resumo da conta (AccountId, DisplayName, Email, CreatedAt)
//   - AccountBalanceView   → Saldo da conta por ativo (Available, Reserved, Total)
using Exchange.Platform.Contracts.ReadModels;

// Módulo de DI da camada Application — registra os serviços de aplicação no container
// Arquivo: libs/dotnet/Exchange.Trading.Application/DependencyInjection.cs
//   Registra:
//     - IInstrumentCatalog      → StaticInstrumentCatalog (catálogo de instrumentos)
//     - ITradingAccountResolver → DemoTradingAccountResolver (resolve contas de trading)
//     - IOrderCommandService    → OrderCommandService (cria/cancela/consulta ordens)
//     - IAccountService         → InMemoryAccountService (CRUD de contas e saldos)
using Exchange.Trading.Application;

// Interfaces dos serviços que a API utiliza via injeção de dependência
// Arquivo: libs/dotnet/Exchange.Trading.Application/Services/IAccountService.cs
//   - IAccountService: CreateAsync, GetByIdAsync, ListAsync, FundAsync, GetBalancesAsync
//   - CreateAccountResult, FundAccountResult (records de resultado definidos no mesmo arquivo)
// Arquivo: libs/dotnet/Exchange.Trading.Application/Services/IOrderCommandService.cs
//   - IOrderCommandService: CreateAsync, CancelAsync, GetByIdAsync, ListAsync
using Exchange.Trading.Application.Services;

// Módulo de DI da camada Infrastructure — registra implementações de infraestrutura
// Arquivo: libs/dotnet/Exchange.Trading.Infrastructure/DependencyInjection.cs
//   Registra:
//     - IOrderRepository         → InMemoryOrderRepository (persiste ordens em memória)
//     - IMatchingEngineClient    → KafkaMatchingEngineClient (envia ordens ao matching engine via Kafka)
//     - IIntegrationEventPublisher → KafkaIntegrationEventPublisher (publica eventos de integração)
using Exchange.Trading.Infrastructure;

namespace Exchange.Gateway.Api;

public class Program
{
    public static void Main(string[] args)
    {
        // =====================================================
        // 1. CONFIGURAÇÃO DO HOST (Builder)
        // =====================================================
        // Cria o builder da aplicação ASP.NET Core.
        // O builder é responsável por configurar serviços (DI),
        // middleware, logging, configuração (appsettings.json) etc.
        var builder = WebApplication.CreateBuilder(args);

        // Configura a serialização JSON para que enums sejam
        // representados como strings legíveis ("Buy", "Sell")
        // em vez de números inteiros (0, 1).
        // Isso melhora a legibilidade das respostas da API.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        // =====================================================
        // 2. REGISTRO DE DEPENDÊNCIAS (Dependency Injection)
        // =====================================================

        // AddTradingApplication() — Registra os serviços da camada Application:
        //   → IInstrumentCatalog (Singleton)       — Catálogo estático de instrumentos financeiros
        //                                            Implementação: StaticInstrumentCatalog
        //                                            Arquivo: Services/StaticInstrumentCatalog.cs
        //   → ITradingAccountResolver (Singleton)  — Resolve qual TradingAccount pertence a uma Account
        //                                            Implementação: DemoTradingAccountResolver
        //                                            Arquivo: Services/DemoTradingAccountResolver.cs
        //   → IOrderCommandService (Singleton)     — Serviço principal de ordens (criar, cancelar, consultar)
        //                                            Implementação: OrderCommandService
        //                                            Arquivo: Services/OrderCommandService.cs
        //   → IAccountService (Singleton)          — Serviço de contas (criar, consultar, depositar, saldos)
        //                                            Implementação: InMemoryAccountService
        //                                            Arquivo: Services/InMemoryAccountService.cs
        builder.Services.AddTradingApplication();

        // AddTradingInfrastructure() — Registra implementações de infraestrutura:
        //   → IOrderRepository (Singleton)           — Repositório de ordens em memória
        //                                              Implementação: InMemoryOrderRepository
        //                                              Arquivo: Repositories/InMemoryOrderRepository.cs
        //   → IMatchingEngineClient (Singleton)      — Cliente que envia ordens ao Matching Engine via Kafka
        //                                              Implementação: KafkaMatchingEngineClient
        //                                              Arquivo: Matching/KafkaMatchingEngineClient.cs
        //   → IIntegrationEventPublisher (Singleton) — Publica eventos de integração (ex: OrderCreated) no Kafka
        //                                              Implementação: KafkaIntegrationEventPublisher
        //                                              Arquivo: Messaging/KafkaIntegrationEventPublisher.cs
        builder.Services.AddTradingInfrastructure();

        // Configura CORS (Cross-Origin Resource Sharing) para permitir
        // que qualquer origem (frontend, mobile, etc.) acesse a API.
        // Em produção, seria restrito a domínios específicos.
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        // =====================================================
        // 3. CONSTRUÇÃO DA APLICAÇÃO
        // =====================================================
        // Após configurar todos os serviços, o Build() cria a instância
        // do WebApplication com o pipeline de middlewares.
        var app = builder.Build();

        // Ativa o middleware de CORS para todas as requisições.
        app.UseCors();

        // =====================================================
        // 4. ENDPOINTS — Health Check
        // =====================================================
        // GET /health
        // Endpoint de verificação de saúde (health check).
        // Utilizado por orquestradores (Docker, Kubernetes) para saber
        // se o serviço está ativo e respondendo.
        // Retorna: { service, status, utcNow }
        app.MapGet("/health", () => Results.Ok(new
        {
            service = "gateway-api",
            status = "ok",
            utcNow = DateTimeOffset.UtcNow
        }));

        // =====================================================
        // 5. ENDPOINTS — Contas (Accounts)
        // =====================================================
        // Estes endpoints gerenciam o ciclo de vida das contas de usuário.
        // O serviço IAccountService é injetado automaticamente pelo container de DI.
        //
        // Interface: IAccountService
        //   Arquivo: libs/dotnet/Exchange.Trading.Application/Services/IAccountService.cs
        // Implementação: InMemoryAccountService
        //   Arquivo: libs/dotnet/Exchange.Trading.Application/Services/InMemoryAccountService.cs

        // POST /api/accounts
        // Cria uma nova conta de usuário na plataforma.
        //
        // Body (JSON): CreateAccountCommand { AccountId, DisplayName, Email, RequestedAt }
        //   Arquivo: libs/contracts/dotnet/Exchange.Platform.Contracts/Commands/CreateAccountCommand.cs
        //
        // Fluxo:
        //   1. Recebe o command deserializado automaticamente do body JSON
        //   2. Chama accountService.CreateAsync() que valida e persiste a conta
        //   3. Se sucesso → retorna HTTP 201 (Created) com os dados da conta
        //   4. Se falha  → retorna HTTP 400 (Bad Request) com o motivo da rejeição
        //
        // Retorno de sucesso: CreateAccountResult { Success, AccountId, DisplayName, Email, CreatedAt, Reason }
        //   Definido em: libs/dotnet/Exchange.Trading.Application/Services/IAccountService.cs
        app.MapPost("/api/accounts", async (CreateAccountCommand command, IAccountService accountService, CancellationToken ct) =>
        {
            var result = await accountService.CreateAsync(command, ct);
            return result.Success
                ? Results.Created($"/api/accounts/{result.AccountId}", new
                {
                    result.AccountId,
                    result.DisplayName,
                    result.Email,
                    result.CreatedAt
                })
                : Results.BadRequest(new { reason = result.Reason });
        });

        // GET /api/accounts/{accountId}
        // Busca uma conta específica pelo seu GUID.
        //
        // Parâmetro de rota: accountId (Guid) — filtrado pelo constraint ":guid"
        //
        // Fluxo:
        //   1. Chama accountService.GetByIdAsync() para buscar a conta
        //   2. Se encontrada → retorna HTTP 200 com AccountSummary
        //   3. Se não existe → retorna HTTP 404 (Not Found)
        //
        // Retorno: AccountSummary { AccountId, DisplayName, Email, CreatedAt }
        //   Arquivo: libs/contracts/dotnet/Exchange.Platform.Contracts/ReadModels/AccountSummary.cs
        app.MapGet("/api/accounts/{accountId:guid}", async (Guid accountId, IAccountService accountService, CancellationToken ct) =>
        {
            var account = await accountService.GetByIdAsync(accountId, ct);
            return account is null ? Results.NotFound() : Results.Ok(account);
        });

        // GET /api/accounts
        // Lista todas as contas cadastradas na plataforma.
        //
        // Retorno: IReadOnlyCollection<AccountSummary>
        //   Cada item contém: AccountId, DisplayName, Email, CreatedAt
        app.MapGet("/api/accounts", async (IAccountService accountService, CancellationToken ct) =>
        {
            var accounts = await accountService.ListAsync(ct);
            return Results.Ok(accounts);
        });

        // =====================================================
        // 6. ENDPOINTS — Depósitos / Funding
        // =====================================================
        // Estes endpoints permitem depositar fundos (dinheiro ou ativos)
        // em uma conta existente e consultar os saldos.

        // POST /api/accounts/{accountId}/fund
        // Deposita fundos em uma conta de usuário.
        //
        // Parâmetro de rota: accountId (Guid) — a conta que receberá os fundos
        // Body (JSON): FundRequest { Asset, Amount, ReferenceId }
        //   → FundRequest é um record local definido ao final deste arquivo (linha ~189)
        //   → É convertido internamente para FundAccountCommand
        //
        // FundAccountCommand { AccountId, Asset, Amount, ReferenceId, RequestedAt }
        //   Arquivo: libs/contracts/dotnet/Exchange.Platform.Contracts/Commands/FundAccountCommand.cs
        //
        // Fluxo:
        //   1. Monta o FundAccountCommand combinando o accountId da rota + dados do body
        //   2. Chama accountService.FundAsync() que valida e credita o saldo
        //   3. Se sucesso → HTTP 200 com o novo saldo disponível
        //   4. Se falha  → HTTP 400 com o motivo
        //
        // Retorno de sucesso: FundAccountResult { Success, NewAvailableBalance, FundedAt, Reason }
        //   Definido em: libs/dotnet/Exchange.Trading.Application/Services/IAccountService.cs
        app.MapPost("/api/accounts/{accountId:guid}/fund", async (Guid accountId, FundRequest request, IAccountService accountService, CancellationToken ct) =>
        {
            var command = new FundAccountCommand(accountId, request.Asset, request.Amount, request.ReferenceId, DateTimeOffset.UtcNow);
            var result = await accountService.FundAsync(command, ct);
            return result.Success
                ? Results.Ok(new
                {
                    accountId,
                    request.Asset,
                    request.Amount,
                    newAvailable = result.NewAvailableBalance,
                    fundedAt = result.FundedAt
                })
                : Results.BadRequest(new { reason = result.Reason });
        });

        // GET /api/accounts/{accountId}/balances
        // Consulta todos os saldos de uma conta, separados por ativo (BRL, BTC, etc.).
        //
        // Retorno: IReadOnlyCollection<AccountBalanceView>
        //   Cada item: { AccountId, Asset, Available, Reserved, Total, AsOf }
        //   Arquivo: libs/contracts/dotnet/Exchange.Platform.Contracts/ReadModels/AccountSummary.cs
        //
        //   - Available = saldo livre para uso
        //   - Reserved  = saldo bloqueado em ordens abertas
        //   - Total     = Available + Reserved
        app.MapGet("/api/accounts/{accountId:guid}/balances", async (Guid accountId, IAccountService accountService, CancellationToken ct) =>
        {
            var balances = await accountService.GetBalancesAsync(accountId, ct);
            return Results.Ok(balances);
        });

        // =====================================================
        // 7. ENDPOINTS — Ordens (Orders)
        // =====================================================
        // Estes endpoints gerenciam ordens de compra/venda.
        // O serviço IOrderCommandService é injetado automaticamente pelo container de DI.
        //
        // Interface: IOrderCommandService
        //   Arquivo: libs/dotnet/Exchange.Trading.Application/Services/IOrderCommandService.cs
        // Implementação: OrderCommandService
        //   Arquivo: libs/dotnet/Exchange.Trading.Application/Services/OrderCommandService.cs
        //
        // O OrderCommandService depende de:
        //   - IOrderRepository         → Persiste ordens (InMemoryOrderRepository)
        //   - IMatchingEngineClient     → Envia ordens ao matching engine (KafkaMatchingEngineClient)
        //   - IInstrumentCatalog        → Valida se o símbolo (ex: PETR4) existe
        //   - ITradingAccountResolver   → Resolve a TradingAccount associada à Account

        // POST /api/orders
        // Cria e submete uma nova ordem ao matching engine.
        //
        // Body (JSON): CreateOrderCommand { OrderId, AccountId, Symbol, Side, Type, Quantity, Price, ... }
        //   Arquivo: libs/contracts/dotnet/Exchange.Platform.Contracts/Commands/CreateOrderCommand.cs
        //
        // Fluxo interno (OrderCommandService.CreateAsync):
        //   1. Busca o instrumento financeiro pelo símbolo (ex: PETR4, VALE3)
        //   2. Resolve a TradingAccount associada ao AccountId
        //   3. Cria a entidade Order no domínio (Domain/Entities/Order.cs)
        //   4. Submete a ordem ao Matching Engine via Kafka
        //   5. Se aceita  → persiste a ordem e retorna os trades gerados
        //   6. Se rejeitada → marca como rejeitada e persiste
        //
        // Retorno de sucesso (HTTP 202 Accepted):
        //   CreateOrderResult { OrderId, Status, RejectionReason, Trades[], Book }
        //   Arquivo: libs/dotnet/Exchange.Trading.Application/Models/CreateOrderResult.cs
        //   - Trades: lista de execuções instantâneas (trade.TradeId, Price, Quantity, ExecutedAt)
        //   - Book: snapshot do livro de ofertas após a execução (Bids, Asks)
        //
        // Retorno de falha (HTTP 400 Bad Request):
        //   { OrderId, Status, Reason }
        app.MapPost("/api/orders", async (CreateOrderCommand command, IOrderCommandService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(command, cancellationToken);
            return result.RejectionReason is null
                ? Results.Accepted($"/api/orders/{result.OrderId}", new
                {
                    result.OrderId,
                    status = result.Status.ToString(),
                    // Trades: execuções que ocorreram imediatamente (matching parcial ou total)
                    // Trade é uma entidade do domínio: libs/dotnet/Exchange.Trading.Domain/Entities/Trade.cs
                    trades = result.Trades.Select(trade => new
                    {
                        trade.TradeId,
                        price = trade.Price.Value,       // Value Object Price → decimal
                        quantity = trade.Quantity.Value,  // Value Object Quantity → decimal
                        trade.ExecutedAt
                    }),
                    // Book: snapshot do livro de ofertas (order book) após a execução
                    // OrderBook é uma entidade do domínio: libs/dotnet/Exchange.Trading.Domain/Entities/OrderBook.cs
                    book = result.Book is null ? null : new
                    {
                        symbol = result.Book.Symbol.Value,
                        // Bids = ofertas de compra (ordenadas do maior para menor preço)
                        bids = result.Book.Bids.Select(level => new { price = level.Price.Value, quantity = level.TotalQuantity.Value, level.OrderCount }),
                        // Asks = ofertas de venda (ordenadas do menor para maior preço)
                        asks = result.Book.Asks.Select(level => new { price = level.Price.Value, quantity = level.TotalQuantity.Value, level.OrderCount }),
                        result.Book.AsOf
                    }
                })
                : Results.BadRequest(new
                {
                    result.OrderId,
                    status = result.Status.ToString(),
                    reason = result.RejectionReason
                });
        });

        // POST /api/orders/{orderId}/cancel
        // Solicita o cancelamento de uma ordem existente.
        //
        // Parâmetro de rota: orderId (Guid)
        // Body (JSON): CancelOrderRequest { AccountId, Symbol, RequestedAt }
        //   → CancelOrderRequest é um record local definido ao final deste arquivo
        //   → É convertido internamente para CancelOrderCommand
        //
        // CancelOrderCommand { OrderId, AccountId, Symbol, RequestedAt }
        //   Arquivo: libs/contracts/dotnet/Exchange.Platform.Contracts/Commands/CancelOrderCommand.cs
        //
        // Fluxo interno (OrderCommandService.CancelAsync):
        //   1. Busca a ordem no repositório pelo orderId
        //   2. Se não encontrada → retorna que não foi possível cancelar
        //   3. Envia pedido de cancelamento ao Matching Engine via Kafka
        //   4. Se o engine confirma → marca a ordem como cancelada e persiste
        //   5. Se o engine rejeita → retorna o motivo
        //
        // Retorno: OrderCancellationResult { Cancelled, Reason }
        //   Arquivo: libs/dotnet/Exchange.Trading.Application/Models/OrderCancellationResult.cs
        app.MapPost("/api/orders/{orderId:guid}/cancel", async (Guid orderId, CancelOrderRequest request, IOrderCommandService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CancelAsync(
                new CancelOrderCommand(orderId, request.AccountId, request.Symbol, request.RequestedAt),
                cancellationToken);

            return result.Cancelled
                ? Results.Ok(new { orderId, status = "cancelled" })
                : Results.BadRequest(new { orderId, status = "rejected", reason = result.Reason });
        });

        // GET /api/orders/{orderId}
        // Busca uma ordem específica pelo seu ID.
        //
        // Retorno: Order (entidade do domínio, projetada como JSON anônimo)
        //   Arquivo: libs/dotnet/Exchange.Trading.Domain/Entities/Order.cs
        //
        // Campos retornados:
        //   - OrderId, AccountId, InstrumentId, TradingAccountId
        //   - Symbol (Value Object → string)
        //   - Side (enum → "Buy" ou "Sell"), Type (enum → "Limit" ou "Market")
        //   - Status (enum → "New", "PartiallyFilled", "Filled", "Cancelled", "Rejected")
        //   - OriginalQuantity, FilledQuantity, LimitPrice
        //   - CreatedAt, UpdatedAt
        app.MapGet("/api/orders/{orderId:guid}", async (Guid orderId, IOrderCommandService service, CancellationToken cancellationToken) =>
        {
            var order = await service.GetByIdAsync(orderId, cancellationToken);
            return order is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    order.OrderId,
                    order.AccountId,
                    order.InstrumentId,
                    order.TradingAccountId,
                    symbol = order.Symbol.Value,          // Value Object Symbol → string
                    side = order.Side.ToString(),          // Enum OrderSide → "Buy" | "Sell"
                    type = order.Type.ToString(),          // Enum OrderType → "Limit" | "Market"
                    status = order.Status.ToString(),      // Enum OrderStatus → "New" | "Filled" | etc.
                    quantity = order.OriginalQuantity.Value,// Value Object Quantity → decimal
                    order.FilledQuantity,
                    price = order.LimitPrice?.Value,        // Value Object Price (nullable) → decimal?
                    order.CreatedAt,
                    order.UpdatedAt
                });
        });

        // GET /api/orders?accountId={guid}
        // Lista todas as ordens, opcionalmente filtradas por conta.
        //
        // Query string (opcional): accountId (Guid)
        //   - Se informado → filtra ordens daquela conta
        //   - Se omitido   → retorna todas as ordens
        //
        // Retorno: IReadOnlyCollection<Order> projetado como lista de objetos JSON
        // Mesmos campos do GET /api/orders/{orderId} acima.
        app.MapGet("/api/orders", async (Guid? accountId, IOrderCommandService service, CancellationToken cancellationToken) =>
        {
            var orders = await service.ListAsync(accountId, cancellationToken);
            return Results.Ok(orders.Select(order => new
            {
                order.OrderId,
                order.AccountId,
                order.InstrumentId,
                order.TradingAccountId,
                symbol = order.Symbol.Value,
                side = order.Side.ToString(),
                type = order.Type.ToString(),
                status = order.Status.ToString(),
                quantity = order.OriginalQuantity.Value,
                order.FilledQuantity,
                price = order.LimitPrice?.Value,
                order.CreatedAt,
                order.UpdatedAt
            }));
        });

        // =====================================================
        // 8. INICIALIZAÇÃO DO SERVIDOR
        // =====================================================
        // app.Run() inicia o servidor Kestrel e começa a escutar
        // requisições HTTP na porta configurada (padrão: 5000/5001).
        // A execução bloqueia aqui até o servidor ser encerrado (Ctrl+C).
        app.Run();
    }

    // =====================================================
    // 9. RECORDS AUXILIARES (DTOs locais da API)
    // =====================================================
    // Estes records são usados apenas na camada da API para
    // deserializar o body de algumas requisições.
    // São diferentes dos Commands porque representam apenas
    // os dados enviados pelo cliente — o endpoint complementa
    // com informações da rota (como orderId ou accountId).

    /// <summary>
    /// DTO para o body da requisição POST /api/orders/{orderId}/cancel.
    /// O orderId vem da rota, não do body — por isso este record não o inclui.
    /// É convertido para CancelOrderCommand pelo endpoint.
    /// </summary>
    public sealed record CancelOrderRequest(Guid AccountId, string Symbol, DateTimeOffset RequestedAt);

    /// <summary>
    /// DTO para o body da requisição POST /api/accounts/{accountId}/fund.
    /// O accountId vem da rota, não do body — por isso este record não o inclui.
    /// É convertido para FundAccountCommand pelo endpoint.
    /// </summary>
    public sealed record FundRequest(string Asset, decimal Amount, string? ReferenceId);
}
