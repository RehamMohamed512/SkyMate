# SkyMate Travel Assistant API

This contract is for the React Web and Flutter Mobile clients. Conversation endpoints require a valid JWT for a user with the `User` role. Send it as `Authorization: Bearer <access-token>`.

Conversation records belong to the authenticated user and are independent of trips. `tripId` is optional on a message request only. The API verifies that the trip belongs to the same user and does not return or persist that ID on the conversation or message.

## Conversations

| Method and route | Purpose | Success |
|---|---|---|
| `POST /api/v1/conversations` | Create an empty conversation. Body: `{ "title": "Planning ideas" }`; `title` is optional and limited to 200 characters. | `201 Created`, `ConversationResponse` |
| `GET /api/v1/conversations` | List the current user's conversations, most recently active first. | `200 OK`, array of `ConversationResponse` |
| `GET /api/v1/conversations/{id}` | Open a conversation header owned by the current user. | `200 OK`, `ConversationResponse` |
| `GET /api/v1/conversations/{id}/messages` | Read messages for an owned conversation. | `200 OK`, array of `ConversationMessageResponse` |
| `POST /api/v1/conversations/{id}/messages` | Send one message and persist the user/assistant pair. | `201 Created`, `ConversationTurnResponse` |
| `DELETE /api/v1/conversations/{id}` | Permanently delete an owned conversation and its messages. | `204 No Content` |

Create request:

```json
{
  "title": "Cairo weekend"
}
```

Conversation response:

```json
{
  "id": 18,
  "title": "Cairo weekend",
  "createdAtUtc": "2026-11-24T09:00:00Z",
  "updatedAtUtc": "2026-11-24T09:00:00Z"
}
```

Send-message request. `content` is trimmed and must contain 1–1,000 characters. `tripId` may be omitted or `null`; when supplied, it is used only for this message:

```json
{
  "content": "What is planned for day one?",
  "tripId": 42
}
```

Successful message response. The `reply` text is generated for the request and is not a fixed value:

```json
{
  "userMessage": {
    "id": 54,
    "role": "User",
    "content": "What is planned for day one?",
    "createdAtUtc": "2026-11-24T09:01:00Z"
  },
  "assistantMessage": {
    "id": 55,
    "role": "Assistant",
    "content": "Here is the itinerary supplied for day one...",
    "createdAtUtc": "2026-11-24T09:01:00Z"
  }
}
```

## Errors

| Status | Meaning and response |
|---|---|
| `400 Bad Request` | Blank message: `{ "code": "message_required" }`. More than 1,000 characters: `{ "code": "message_too_long", "max": 1000 }`. Invalid `tripId` values may return ASP.NET `ValidationProblemDetails`. |
| `401 Unauthorized` | Missing or invalid JWT, or a token without a valid integer `sub` claim. |
| `403 Forbidden` | Authenticated caller does not have the `User` role. |
| `404 Not Found` | Conversation or supplied trip is missing or not owned by the caller. The conversation endpoints return an empty `404` response. |
| `429 Too Many Requests` | Application limit: `{ "code": "assistant_rate_limited" }` after 10 messages per minute per authenticated user. Provider quota errors are returned as `ProblemDetails` with title `Assistant rate limit reached`. |
| `502 Bad Gateway` | Gemini/provider request failed; safe `ProblemDetails` title is `Assistant provider failed`. |
| `503 Service Unavailable` | Provider configuration is missing or invalid; safe title is `Assistant configuration is missing`. |
| `504 Gateway Timeout` | Provider did not respond before the configured timeout; safe title is `Assistant provider timed out`. |

The API does not expose provider credentials or continuation metadata in these responses. The backend sends the current user message, recent conversation history, system instructions, and an authorized trip context when supplied to the configured AI provider. The backend does not add account email or phone fields to that request; personal information typed into a chat message may still be sent.

General travel answers are disabled by default (`AssistantAi:AllowGeneralTravelQuestions=false`). When enabled, answers must be labeled as general advice and must not state prices or opening hours as facts.

## Administrator knowledge base

These routes require the `Admin` role and are not used for ordinary conversation flows:

- `GET /api/v1/admin/assistant/knowledge-base` and `GET /{id}` list or read entries.
- `POST /api/v1/admin/assistant/knowledge-base` creates an entry (`201 Created`).
- `PUT /api/v1/admin/assistant/knowledge-base/{id}` updates an entry (`200 OK`).
- `DELETE /api/v1/admin/assistant/knowledge-base/{id}` deactivates an entry (`204 No Content`).
- `POST /api/v1/admin/assistant/knowledge-base/seed-defaults` inserts missing defaults without overwriting administrator edits.

For a `POST` or `PUT`, send `{ "title": "...", "content": "...", "isActive": true }`. Titles are limited to 200 characters and content to 4,000 characters. Duplicate titles return `409 Conflict`; invalid entries return `400 Bad Request`.
