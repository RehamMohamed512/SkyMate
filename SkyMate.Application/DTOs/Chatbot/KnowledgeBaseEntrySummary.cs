namespace SkyMate.Application.DTOs.Chatbot;

/// <summary>Knowledge-base item returned to an authorized administrator.</summary>
public sealed record KnowledgeBaseEntrySummary(int Id, string Title, string Content, bool IsActive);

/// <summary>Fields an administrator can edit on a knowledge-base item.</summary>
public sealed record KnowledgeBaseEntryUpdate(string Title, string Content, bool IsActive);

/// <summary>Count of defaults inserted without replacing existing entries.</summary>
public sealed record KnowledgeBaseSeedResult(int Added, int AlreadyPresent);
