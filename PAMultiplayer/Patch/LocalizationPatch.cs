using System;
using Il2CppInterop.Runtime.Injection;
using UnityEngine.Localization.Tables;

namespace PAMultiplayer.Patch;

//made by Enchart
//I honestly don't remember why I needed this

[Serializable]
public class TablePostprocessor : Il2CppSystem.Object
{
    public TablePostprocessor() : base(ClassInjector.DerivedConstructorPointer<TablePostprocessor>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    public void PostprocessTable(LocalizationTable table)
    {
        // DO NOT use conditional cast, like `if (table is StringTable stringTable)`, it will NOT work for some reason
        var stringTable = table.TryCast<StringTable>();
        if (stringTable == null) return;

        // PA's localization table is named 'Localization'
        if (!stringTable.name.Contains("Localization")) return;

        // this is where you actually start doing stuff
        // e.g. add a new localization entry, key can be anything you want
        const string key = "ui.multiplayer.update";
        stringTable.AddEntry(key, "<sprite name=info> Update Multiplayer");
        
    }
}