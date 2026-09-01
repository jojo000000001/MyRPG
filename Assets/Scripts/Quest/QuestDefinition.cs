using System;

/// <summary>
/// 一条任务的静态描述。
/// </summary>
[Serializable]
public sealed class QuestDefinition
{
    public string id;
    public string title;
    public string objectiveLabel;
    public int requiredCount = 1;

    public static QuestDefinition HuntInvadingGoblin()
    {
        return new QuestDefinition
        {
            id = QuestIds.HuntInvadingGoblin,
            title = "讨伐入侵的哥布林",
            objectiveLabel = "击败前来入侵的哥布林",
            requiredCount = 1
        };
    }
}
