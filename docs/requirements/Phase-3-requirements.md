# LifeGrid - Phase 3 Infrastructure Requirements
## Secure AI Credential Initializer Baseline

This document specifies the structural requirements for Phase 3. The target objective is an infrastructure-level preparatory slice implementing a secure, build-time API key injection handoff to local OS-encrypted secure storage. No live external network operations or feature screens are active during this phase.

---

## 1. Local Workspace Secret Protections

* **User Secrets Architecture:** Implement local secret isolation using the native .NET User Secrets Manager within the `LifeGrid.Infrastructure` project workspace. 
* **Git Invariant:** Ensure raw API configuration strings or plaintext token files are strictly barred from source control by enforcing existing project `.gitignore` rules.

---

## 2. Compilation Token Injection Pipeline

* **Build-Time Extraction:** Configure the project build properties or compilation engine to securely pull the `Gemini:ApiKey` configuration variable from local environment/user secrets during the compilation phase.
* **In-Memory Obfuscation:** The token must be processed or mapped inside an internal compilation utility class (e.g., `BuildSecretProvider`) as an obfuscated sequence or byte array, avoiding raw plain text string literals in compiled assemblies.

---

## 3. First-Launch Synchronization Engine (TDD Enforced)

* **Launch Interception Loop:** Implement an asynchronous startup initialization check running sequentially before the onboarding views map states:
  1. Query the device's native encrypted workspace via `SecureStorage` for the record tracking identifier: `Gemini_Provider_Token`.
  2. **Case A (Record Exists):** Safe termination of initialization loop. The pipeline bypasses compiled memory variables completely.
  3. **Case B (Record Missing):** Execute the one-time synchronization handshake:
     * Access the obfuscated compiled variable from the compilation pipeline.
     * Commit the de-obfuscated plaintext token string directly to `SecureStorage` under the alias `Gemini_Provider_Token`.
     * Zero-out or dereference the local volatile de-obfuscated memory segment instantly.

---

## 4. Test-Driven Development (TDD) Invariants

* **Verification Coverage:** Build comprehensive unit tests with `xUnit` and isolation tools to guarantee 100% logic coverage of the synchronization loop rules.
* **Strict Execution Bounds:** Using a mocked target interface for `SecureStorage`, test parameters must rigorously assert that:
  * The synchronization process writes to secure storage exactly once on a verified clean environment.
  * Subsequent initialization checks read from the mock secure registry and do *not* touch or invoke the build-time provider logic again.