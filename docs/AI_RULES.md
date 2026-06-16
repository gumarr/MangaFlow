# AI Development Rules

Read this file before making any code changes.

---

# Architecture Rules

Always follow Clean Architecture.

Layers:

Domain
Application
Infrastructure
UI

Dependencies must point inward.

UI must never directly access Infrastructure.

---

# SOLID Rules

All code must follow SOLID principles.

Avoid God Classes.

Avoid Service Locator.

Prefer dependency injection.

---

# Performance Rules

This application targets local AI inference.

Performance is critical.

Avoid:

* unnecessary allocations
* blocking UI thread
* loading models repeatedly

Models must be loaded once and reused.

---

# OCR Rules

OCR implementation must be abstracted.

Never depend directly on PaddleOCR.

Always use interfaces.

Example:

IOcrService

This allows future OCR engine replacement.

---

# AI Model Rules

AI model implementation must be abstracted.

Never depend directly on Qwen.

Never depend directly on llama.cpp.

Use interfaces.

Example:

ITranslationEngine

Future engines must be swappable.

---

# Database Rules

SQLite only.

Never place SQL inside UI layer.

Repositories belong to Infrastructure.

---

# UI Rules

Use MVVM.

No business logic inside Views.

Keep ViewModels small.

---

# Memory Rules

Translation memory is a first-class feature.

Never bypass:

Global Memory
Series Memory
Recent Context

All translations must pass through memory layers.

---

# Error Handling Rules

No silent failures.

All errors must:

* be logged
* be recoverable
* provide user feedback

---

# Logging Rules

Structured logging only.

Use Microsoft.Extensions.Logging.

No Console.WriteLine.

---

# Testing Rules

Critical logic requires tests.

Required:

* Memory System
* Glossary System
* Reading Order Engine
* Prompt Builder

---

# Security Rules

No arbitrary code execution.

No shell command execution without abstraction.

All downloaded models must be checksum verified.

---

# Future Compatibility

All external services must be replaceable.

Assume:

OCR engine will change.
LLM engine will change.
UI framework may evolve.

Design for replacement.

---

# Golden Rule

Prefer maintainability over cleverness.

Prefer simple solutions over AI-heavy solutions.

The user should always remain in control.
