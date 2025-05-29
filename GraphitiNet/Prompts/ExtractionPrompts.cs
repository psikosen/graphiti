namespace GraphitiNet.Prompts;

internal static class ExtractionPrompts
{
    // Using C# 11 Raw String Literals for clarity
    public const string EntityExtractionTemplate = """
    You are an AI assistant specialized in extracting entities from text.
    Your task is to identify and classify entities based on the provided INPUT TEXT and a list of TARGET ENTITY LABELS.

    INPUT TEXT:
    ---
    {inputText}
    ---

    TARGET ENTITY LABELS:
    ---
    {targetEntityLabelsJsonArray}
    ---
    (Example: ["Person", "Organization", "Location", "Product", "Event"])

    Instructions:
    1. Analyze the INPUT TEXT carefully.
    2. Identify all significant entities that match any of the TARGET ENTITY LABELS.
    3. For each identified entity, determine its most appropriate label from the TARGET ENTITY LABELS.
    4. If an entity could fit multiple labels, choose the most specific or primary one.
    5. Ensure the 'name' of the entity is extracted as accurately as possible from the text.
    6. Do NOT extract entities that do not fit any of the TARGET ENTITY LABELS.
    7. Do NOT extract attributes of entities, only their names and assigned labels.

    Output Format:
    Return a single JSON array of objects. Each object must have the following fields:
    - "name": string (The extracted name of the entity)
    - "label": string (The assigned label for the entity, must be one of the TARGET ENTITY LABELS)

    Example JSON Output:
    [
      { "name": "Acme Corp", "label": "Organization" },
      { "name": "John Doe", "label": "Person" },
      { "name": "New York", "label": "Location" }
    ]

    If no entities are found matching the criteria, return an empty JSON array [].

    Provide only the JSON array in your response.
    """;

    // Placeholder for RelationshipExtractionTemplate - Not implemented in this task, but shown for context
    // This template would be used in a subsequent task (e.g., Task 4.3 for relationship extraction)
    public const string RelationshipExtractionTemplate = """
    You are an AI assistant specialized in extracting relationships between entities from text.
    Your task is to identify relationships based on the provided INPUT TEXT, a list of PRE-EXTRACTED ENTITIES,
    a list of TARGET RELATIONSHIP TYPES, and a REFERENCE TIME.

    INPUT TEXT:
    ---
    {inputText}
    ---

    PRE-EXTRACTED ENTITIES (Name and Label):
    ---
    {extractedEntitiesJsonArray}
    ---
    (Example: [{ "name": "Alice Wonderland", "label": "Person" }, { "name": "Mad Hatter Tea Inc.", "label": "Organization" }])

    TARGET RELATIONSHIP TYPES:
    ---
    {targetRelationshipTypesJsonArray}
    ---
    (Example: ["WORKS_FOR", "LIVES_IN", "FOUNDED_ON", "ATTENDED_EVENT"])

    REFERENCE TIME (ISO 8601 UTC for resolving temporal expressions):
    ---
    {referenceTimeIso}
    ---

    Instructions:
    1. Analyze the INPUT TEXT to find connections between the PRE-EXTRACTED ENTITIES.
    2. Only extract relationships where both the source and target entities are present in the PRE-EXTRACTED ENTITIES list.
    3. The `relationship_type` must be one of the TARGET RELATIONSHIP TYPES.
    4. The `fact` should be a concise natural language sentence describing the relationship as found in the text.
    5. Determine `valid_at` (when the relationship began or occurred) and `invalid_at` (when the relationship ended, if applicable) based on the INPUT TEXT and the REFERENCE TIME.
       - If a date/time is relative (e.g., "last year", "tomorrow"), resolve it against the REFERENCE TIME.
       - If no specific start or end time is mentioned, these fields can be null.
       - Dates should be in ISO 8601 format (YYYY-MM-DD or YYYY-MM-DDTHH:MM:SSZ). If time is absent, use start of day (00:00:00Z).
    6. Do NOT create entities. Only use the provided PRE-EXTRACTED ENTITIES.

    Output Format:
    Return a single JSON array of objects. Each object must have the following fields:
    - "source_entity_name": string (Name of the source entity, must match one from PRE-EXTRACTED ENTITIES)
    - "relationship_type": string (Type of the relationship, must match one from TARGET RELATIONSHIP TYPES)
    - "target_entity_name": string (Name of the target entity, must match one from PRE-EXTRACTED ENTITIES)
    - "fact": string (Concise natural language description of the relationship)
    - "valid_at": string (ISO 8601 date or datetime, or null)
    - "invalid_at": string (ISO 8601 date or datetime, or null)

    Example JSON Output:
    [
      {
        "source_entity_name": "Alice Wonderland",
        "relationship_type": "WORKS_FOR",
        "target_entity_name": "Mad Hatter Tea Inc.",
        "fact": "Alice Wonderland started working for Mad Hatter Tea Inc. last month.",
        "valid_at": "2023-09-15T00:00:00Z", // Assuming referenceTime was 2023-10-15
        "invalid_at": null
      }
    ]

    If no relationships are found matching the criteria, return an empty JSON array [].

    Provide only the JSON array in your response.
    """;
}
```
