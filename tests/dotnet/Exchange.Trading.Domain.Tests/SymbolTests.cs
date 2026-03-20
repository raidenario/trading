using Exchange.Trading.Domain.ValueObjects;
using Xunit;

namespace Exchange.Trading.Domain.Tests;

public sealed class SymbolTests
{
    [Fact]
    public void Symbol_Normalizes_To_Uppercase()
    {
        var symbol = new Symbol(" btc-usd ");

        Assert.Equal("BTC-USD", symbol.Value);
    }
}
