namespace SkyMate.Application.Interfaces.Chatbot;

using SkyMate.Application.AI;

/// <summary>Builds the system instruction for SkyMate's general travel assistant.</summary>
public interface IAssistantPromptBuilder
{
    /// <summary>Builds the approved system prompt with active knowledge and optional per-message trip context.</summary>
    string BuildSystemInstruction(AssistantPromptContext context);
}
