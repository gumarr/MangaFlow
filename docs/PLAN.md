# MangaFlow - Product Plan

## Vision

MangaFlow is a local-first desktop application that allows users to instantly translate manga, manhwa, webtoons, light novels, visual novels and screenshots using OCR and local AI.

The application should be simple enough for non-technical users:

Install → Download Model → Start Reading

No terminal usage required.

---

# Target Users

Primary:

* Manga readers
* Light novel readers
* Visual novel readers

Secondary:

* Webtoon readers
* Language learners
* Translators

---

# Core Principles

1. Local-first
2. Fast response
3. User-controlled context
4. Consistent terminology
5. Minimal setup

---

# Feature Set

## Bubble Translation Mode

Workflow:

Hotkey
→ Select region
→ OCR
→ Translation Memory
→ Context Memory
→ LLM Translation
→ Popup Result

Target:

< 2 seconds

---

## Full Page Translation Mode

Workflow:

Select page
→ OCR all regions
→ Reading order heuristic
→ Translation Memory
→ Context Memory
→ Translation
→ Overlay

Target:

< 5 seconds

---

# OCR

Primary OCR Engine:

PaddleOCR

Languages:

* Japanese
* English
* Chinese
* Korean
* Vietnamese

Requirements:

* Horizontal text
* Vertical Japanese text

---

# AI Models

Default:

Qwen 3 8B GGUF

Optional:

* Qwen 4B
* Qwen 14B

Inference:

llama.cpp

---

# Memory System

Three layers:

## Global Memory

Shared across all projects.

Examples:

勇者 → Dũng Giả
魔王 → Ma Vương

---

## Series Memory

Specific to a manga series.

Examples:

One Piece
Overlord
Re:Zero

Each project has independent terminology.

---

## Recent Context Memory

Stores last 10-20 translated bubbles.

Used as translation context.

---

# Glossary Manager

Users can:

* Add terms
* Edit terms
* Lock terms
* Import terms
* Export terms

Locked terms must always be respected.

---

# Project System

Each series is a project.

A project contains:

* Glossary
* Translation History
* Recent Context
* Settings

---

# Installer

Installer should remain lightweight.

Models are downloaded after installation.

First launch wizard:

* Basic
* Recommended
* High Quality

Models are downloaded automatically.

---

# Technology Stack

Frontend:

* WinUI 3

Backend:

* .NET 9
* C#

Database:

* SQLite

OCR:

* PaddleOCR

Inference:

* llama.cpp

---

# Milestones

## Phase 1

* Screen Capture
* OCR
* Translation

## Phase 2

* Translation Memory
* Context Memory

## Phase 3

* Glossary Manager

## Phase 4

* Full Page Mode

## Phase 5

* Model Downloader

## Phase 6

* Public Beta

---

# Success Metrics

Bubble Translation:

< 2s

Full Page Translation:

< 5s

Memory Accuracy:

Consistent terminology across entire series.

Installation:

User can start translating within 10 minutes after download.
