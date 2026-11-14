using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Mulld.Priorities;

public static class NamedPresetTests
{
    public static string Run(string artifacts)
    {
        int checks=0;
        void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        string folder=Path.Combine(artifacts,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"presets.xml");
        string Validate(XElement xml)=>xml.Name=="mulldPriorities"&&(int?)xml.Attribute("value")>=0?null:"Invalid payload";
        var first=new XElement("mulldPriorities",new XAttribute("value",1));
        var second=new XElement("mulldPriorities",new XAttribute("value",2));
        var store=new NamedPresetStore(path,Validate);
        Check(store.Error==null&&!store.Names.Any(),"Missing library starts empty");
        Check(NamedPresetStore.ValidateName(" ")!=null&&NamedPresetStore.ValidateName(new string('x',41))!=null&&NamedPresetStore.ValidateName("Line\nBreak")!=null,"Preset name validation");
        Check(store.Save("  Test  ",first,false)==null&&store.Contains("test"),"Save trims names and compares without case");
        string original=File.ReadAllText(path);
        Check(store.Save("TEST",second,false)!=null&&File.ReadAllText(path)==original,"Unconfirmed overwrite preserves file");
        Check(store.Save("TEST",second,true)==null&&store.Names.Single()=="Test","Confirmed overwrite replaces without duplicate or name drift");
        var loaded=new NamedPresetStore(path,Validate);
        Check(loaded.TryGet("test",out var rules)&&(int?)rules.Attribute("value")==2,"Saved rules survive new store instance");
        rules.SetAttributeValue("value",99);loaded.TryGet("Test",out rules);
        Check((int?)rules.Attribute("value")==2,"Returned definition cannot mutate stored preset");
        Check(store.Save("Night shift",first,false)==null&&store.Names.Count()==2,"Save another named preset");
        Check(store.Save("../Outside",first,false)==null&&!File.Exists(Path.Combine(folder,"..","Outside")),"Preset names never become file paths");
        Check(!Directory.GetFiles(folder,"*.tmp-*").Any(),"Atomic saves leave no staging files");
        original=File.ReadAllText(path);
        Check(store.Save("Bad",new XElement("wrong"),false)!=null&&File.ReadAllText(path)==original,"Invalid rules do not change library");
        File.WriteAllText(path,"<mulldPresetLibrary version='1'><preset name='Test'><mulldPriorities value='1'/></preset><preset name='test'><mulldPriorities value='2'/></preset></mulldPresetLibrary>");
        loaded=new NamedPresetStore(path,Validate);
        Check(loaded.Error!=null&&loaded.Save("Replacement",first,true)!=null,"Duplicate/corrupt library cannot be silently overwritten");
        File.WriteAllText(path,"<!DOCTYPE x [<!ENTITY ext SYSTEM 'file:///nonexistent'>]><mulldPresetLibrary version='1' />");
        Check(new NamedPresetStore(path,Validate).Error!=null,"Preset library rejects DTD input");
        return checks+" named preset checks passed.";
    }
}
