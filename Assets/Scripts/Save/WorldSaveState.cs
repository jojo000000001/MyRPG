using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 采集并还原场景里的哥布林、巨龙和犬骑士。
/// 营地哥布林用物体名匹配（如 Goblin_1）；入侵者、林边哨兵、巨龙、骑士用固定 id。
/// </summary>
public static class WorldSaveState
{
    public const string InvadingGoblinId = "InvadingGoblin";
    public const string ForestEdgeGoblinId = "ForestEdgeGoblin";
    public const string DragonId = "DragonBoss";
    public const string KnightId = "DogKnightNpc";

    /// <summary>从当前场景拍一份世界快照。</summary>
    public static WorldSaveData Capture()
    {
        var actors = new List<WorldActorSaveData>(16);

        Goblin[] goblins = Object.FindObjectsOfType<Goblin>();
        for (int i = 0; i < goblins.Length; i++)
            CaptureActor(actors, goblins[i], goblins[i].name);

        DragonBoss dragon = Object.FindObjectOfType<DragonBoss>();
        if (dragon != null)
            CaptureActor(actors, dragon, DragonId);

        DogKnightNpc knight = Object.FindObjectOfType<DogKnightNpc>();
        if (knight != null)
        {
            Transform transform = knight.transform;
            actors.Add(new WorldActorSaveData
            {
                id = KnightId,
                alive = true,
                hp = 1,
                x = transform.position.x,
                y = transform.position.y,
                z = transform.position.z,
                rotY = transform.eulerAngles.y,
            });
        }

        DemoGameManager demo = DemoGameManager.Instance;
        DragonBoss livingDragon = dragon;

        return new WorldSaveData
        {
            actors = actors.ToArray(),
            dragonDescended = livingDragon != null && livingDragon.HasLandedOrDescended,
            demoEnded = demo != null && demo.HasEnded,
        };
    }

    /// <summary>
    /// 按 id 把快照套回场景。已死亡的单位静默移除，不掉落、不加经验。
    /// 入侵哥布林和林边哨兵可能此时还没刷出，由任务系统随后补刷再单独套用。
    /// </summary>
    public static void Apply(WorldSaveData data)
    {
        if (data == null)
            return;

        Dictionary<string, WorldActorSaveData> byId = IndexActors(data.actors);

        Goblin[] goblins = Object.FindObjectsOfType<Goblin>();
        for (int i = 0; i < goblins.Length; i++)
        {
            Goblin goblin = goblins[i];
            if (goblin == null)
                continue;

            if (!byId.TryGetValue(goblin.name, out WorldActorSaveData saved))
                continue;

            ApplyMonster(goblin, saved);
        }

        if (byId.TryGetValue(DragonId, out WorldActorSaveData dragonSave))
        {
            DragonBoss dragon = Object.FindObjectOfType<DragonBoss>();
            if (dragon != null)
            {
                if (!dragonSave.alive)
                    dragon.RestoreAsDead();
                else
                {
                    ApplyMonster(dragon, dragonSave);
                    if (data.dragonDescended)
                        dragon.BeginDescent();
                }
            }
        }
        else if (data.dragonDescended)
        {
            DragonBoss dragon = Object.FindObjectOfType<DragonBoss>();
            dragon?.BeginDescent();
        }

        if (byId.TryGetValue(KnightId, out WorldActorSaveData knightSave))
        {
            DogKnightNpc knight = Object.FindObjectOfType<DogKnightNpc>();
            if (knight != null)
            {
                Vector3 position = new Vector3(knightSave.x, knightSave.y, knightSave.z);
                knight.RestoreSavedPose(position, knightSave.rotY);
            }
        }

        DemoGameManager demo = DemoGameManager.Instance;
        if (demo != null)
            demo.RestoreEnded(data.demoEnded);
    }

    /// <summary>按固定 id 取一条单位记录，给任务补刷后的入侵者/林边哨兵用。</summary>
    public static WorldActorSaveData FindActor(WorldSaveData data, string id)
    {
        if (data?.actors == null || string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < data.actors.Length; i++)
        {
            WorldActorSaveData actor = data.actors[i];
            if (actor != null && actor.id == id)
                return actor;
        }

        return null;
    }

    private static void CaptureActor(List<WorldActorSaveData> actors, Monster monster, string id)
    {
        if (monster == null || string.IsNullOrEmpty(id))
            return;

        Transform transform = monster.transform;
        actors.Add(new WorldActorSaveData
        {
            id = id,
            alive = !monster.IsDead,
            hp = monster.CurrentHp,
            x = transform.position.x,
            y = transform.position.y,
            z = transform.position.z,
            rotY = transform.eulerAngles.y,
        });
    }

    private static void ApplyMonster(Monster monster, WorldActorSaveData saved)
    {
        if (monster == null || saved == null)
            return;

        if (!saved.alive)
        {
            monster.RemoveForSaveRestore();
            return;
        }

        monster.SetCurrentHp(saved.hp);
        CharacterController controller = monster.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        monster.transform.SetPositionAndRotation(
            new Vector3(saved.x, saved.y, saved.z),
            Quaternion.Euler(0f, saved.rotY, 0f));

        if (controller != null)
            controller.enabled = true;
    }

    private static Dictionary<string, WorldActorSaveData> IndexActors(WorldActorSaveData[] actors)
    {
        var map = new Dictionary<string, WorldActorSaveData>();
        if (actors == null)
            return map;

        for (int i = 0; i < actors.Length; i++)
        {
            WorldActorSaveData actor = actors[i];
            if (actor == null || string.IsNullOrEmpty(actor.id))
                continue;

            map[actor.id] = actor;
        }

        return map;
    }
}
