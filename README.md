# Regulated AI Action Workflow Engine

A deliberately small .NET 10 workflow slice for a regulated action: deciding whether Vendor X may be marked approved to process payment data. It retrieves tenant-scoped evidence, applies deterministic risk rules, verifies a recorded human approval, audits every attempt, and executes exactly once only after every gate passes.

No LLM runs in the application. Evidence or a future model may produce candidate facts, but deterministic Core policy remains authoritative over approval and execution.