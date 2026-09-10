using System;
using UnityEngine;

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

    public static QuestDefinition DefeatAllGoblins(int requiredCount)
    {
        return new QuestDefinition
        {
            id = QuestIds.DefeatAllGoblins,
            title = "清剿林地哥布林",
            objectiveLabel = "击败所有哥布林",
            requiredCount = Mathf.Max(1, requiredCount)
        };
    }

    public static QuestDefinition SlayDragon()
    {
        return new QuestDefinition
        {
            id = QuestIds.SlayDragon,
            title = "讨伐巨龙",
            objectiveLabel = "击败树桩上的巨龙",
            requiredCount = 1
        };
    }
}
