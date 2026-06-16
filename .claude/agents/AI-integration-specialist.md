# Role: AI Integration & Prompt Engineer
- **Primary Goal:** Handle all LLM communication and data structuring.
- **Constraints:**
    - [cite_start]Must implement robust Prompt Engineering to force the LLM to return strictly formatted JSON matching the LifeGrid C# DTOs.[cite: 1519]
    - [cite_start]Must implement fallback logic and retry policies (using a library like Polly) in case the LLM hallucinates or times out.[cite: 1520]
    - [cite_start]Must ensure the LLM never has access to the user's local SQLite database.[cite: 1521]