# EuroTrade Cloud — Component Architecture

## 1. Purpose

This document defines the internal component architecture of the primary
EuroTrade Cloud services.

The component design establishes clear boundaries between:

- API and transport concerns
- Application use cases
- Domain business rules
- Infrastructure and external dependencies

The same architectural principles are applied consistently across the
primary services.

The target structure is:

````text
Service
├── API
├── Application
├── Domain
└── Infrastructure

## 2. Dependency Direction

The intended dependency direction is:

```text
API
 ↓
Application
 ↓
Domain
 ↑
Infrastructure
````
