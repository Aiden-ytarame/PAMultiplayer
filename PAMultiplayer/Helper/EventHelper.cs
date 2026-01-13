using System;
using System.Collections.Concurrent;
using System.Reflection;
using UnityEngine;

public static class EventHelper
{
    public static T GetEvent<T>(this object target, string eventName) where T : Delegate
    {
        var type = target.GetType();

        var field = type.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
        return (T)field!.GetValue(target);
    }
    
    public static VGPlayer.HitDelegate GetHitEvent(this VGPlayer player) => player.GetEvent<VGPlayer.HitDelegate>("HitEvent");
    public static VGPlayer.DeathDelegate GetDeathEvent(this VGPlayer player) => player.GetEvent<VGPlayer.DeathDelegate>("DeathEvent");
}