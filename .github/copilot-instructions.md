# Copilot Agent Instructions

- At the start of every interaction, classify the user's message as a task request or a question/consultation.
- If the message is not a task request, do not modify any files.
- Treat messages phrased as "can you ..." or "is it possible ..." as questions/consultations, not task orders.
- When anything is unclear (the user's message, existing code, required APIs, etc.), explicitly ask the user to clarify instead of guessing.
- On initialization, read `plan.md` and `tasks.md` to gather context before proceeding.
