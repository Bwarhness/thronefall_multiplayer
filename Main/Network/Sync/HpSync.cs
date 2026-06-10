using System;
using System.Collections.Generic;
using Steamworks;
using ThronefallMP.Components;
using ThronefallMP.Network.Packets;
using ThronefallMP.Network.Packets.Sync;
using ThronefallMP.Patches;
using ThronefallMP.Utils;
using UnityEngine;

namespace ThronefallMP.Network.Sync;

public class HpSync : BaseTargetSync
{
    protected override bool HandleDisabledTargets => true;
    protected override float ForceUpdateTimer => 3f;

    protected override IEnumerable<(IdentifierData id, GameObject target)> Targets()
    {
        foreach (var data in Identifier.GetIdentifiers(IdentifierType.Player))
        {
            yield return (new IdentifierData{ Type = IdentifierType.Player, Id = data.id}, data.target);
        }
        
        foreach (var data in Identifier.GetIdentifiers(IdentifierType.Building))
        {
            yield return (new IdentifierData{ Type = IdentifierType.Building, Id = data.id}, data.target);
        }
        
        foreach (var data in Identifier.GetIdentifiers(IdentifierType.Ally))
        {
            yield return (new IdentifierData{ Type = IdentifierType.Ally, Id = data.id}, data.target);
        }
        
        foreach (var data in Identifier.GetIdentifiers(IdentifierType.Enemy))
        {
            yield return (new IdentifierData{ Type = IdentifierType.Enemy, Id = data.id}, data.target);
        }
        
        foreach (var data in Identifier.GetIdentifiers(IdentifierType.Enemy))
        {
            yield return (new IdentifierData{ Type = IdentifierType.Enemy, Id = data.id}, data.target);
        }
        
        foreach (var data in Identifier.GetDestroyed(IdentifierType.Enemy))
        {
            yield return (new IdentifierData{ Type = IdentifierType.Enemy, Id = data}, null);
        }
    }

    protected override BasePacket CreateSyncPacket(CSteamID peer, IdentifierData id, GameObject target)
    {
        if (target != null)
        {
            Hp hp = target.GetComponent<Hp>();
            if (hp != null) {
                return new SyncHpPacket
                {
                    Target = id,
                    Hp = hp.HpValue,
                    MaxHp = hp.maxHp,
                    KnockedOut = hp.KnockedOut,
                    Invulnerable = hp.invulnerable
                };
            }
        }
        return new SyncHpPacket
        {
            Target = id,
            Hp = int.MinValue,
            MaxHp = 10,
            KnockedOut = true,
            Invulnerable = false
        };
    }

    protected override bool Compare(CSteamID peer, IdentifierData id, GameObject target, BasePacket current, BasePacket last)
    {
        var a = (SyncHpPacket)current;
        var b = (SyncHpPacket)last;
        return Math.Abs(a.Hp - b.Hp) < Helpers.Epsilon
            && Math.Abs(a.MaxHp - b.MaxHp) < Helpers.Epsilon
            && a.KnockedOut == b.KnockedOut
            && a.Invulnerable == b.Invulnerable;
    }

    public override bool CanHandle(BasePacket packet)
    {
        return packet.TypeID is SyncHpPacket.PacketID;
    }

    public override void Handle(CSteamID peer, BasePacket packet)
    {
        var sync = (SyncHpPacket)packet;
        var target = sync.Target.Get();
        if (target == null)
        {
            return;
        }

        var hp = target.GetComponent<Hp>();
        if (hp.TaggedObj == null)
        {
            return;
        }
        
        hp.maxHp = sync.MaxHp;
        var difference = sync.Hp - hp.HpValue;
        HpPatch.AllowHealthChangeOnClient = true;
        // Client-local invulnerability windows (boss phase coroutines, dash i-frames, shields)
        // make Hp.TakeDamage drop authoritative corrections, including the int.MinValue destroy
        // for enemies already dead on the host. Clear the flag while applying the correction,
        // then adopt the host's value below.
        hp.invulnerable = false;
        if (difference > 0f)
        {
            // If we are not active then don't revive/heal.
            if (!hp.gameObject.activeInHierarchy) {}
            else if (hp.KnockedOut && !sync.KnockedOut)
            {
                hp.Revive(true, sync.Hp / hp.maxHp);
                if (sync.Target.Type == IdentifierType.Ally)
                {
                    var component = hp.GetComponent<PathfindMovementPlayerunit>();
                    component.SnapToNavmesh();
                }
            }
            else if (!sync.KnockedOut)
            {
                hp.Heal(difference);
            }
        }
        else
        {
            // _damageComingFrom does not matter as it calls a pathfinding function that is only handled on the server.
            // causedByPlayer and invokeFeedbackEvents handled by DamageFeedbackPacket
            hp.TakeDamage(-difference, invokeFeedbackEvents: false);
        }

        hp.invulnerable = sync.Invulnerable;
        HpPatch.AllowHealthChangeOnClient = false;
    }
}