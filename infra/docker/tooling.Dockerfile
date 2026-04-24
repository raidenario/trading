FROM python:3.11-slim

WORKDIR /workspace

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

COPY apps/tooling/pyproject.toml apps/tooling/README.md /tmp/tooling/
COPY apps/tooling/src /tmp/tooling/src

RUN python -m pip install --no-cache-dir --upgrade pip && \
    python -m pip install --no-cache-dir /tmp/tooling

COPY . /workspace

CMD ["exchange-tooling", "--help"]
