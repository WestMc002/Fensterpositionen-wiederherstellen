// Fensterpositionen wiederherstellen – Programmeinträge, Einstellungen, Protokolldateien und Selbsttest
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Drawing.Drawing2D;

namespace Fensterpositionen {
public class Entry {
 public string Id, Name, ProcessName, AltProcessName, Exe, Args, File, RegistryPath;
 public string[] Keys;
 public bool Registry;
 // WindowOnly: Position wird direkt am offenen Fenster gemessen und gesetzt (keine Einstellungsdatei).
 public bool WindowOnly;
 // Custom: vom Benutzer über "Programm hinzufügen" angelegt (gespeichert in Positionen.json unter "_custom").
 public bool Custom;
}
public static class Store {
 // Einstellungsordner: %LOCALAPPDATA%\Fensterpositionen wiederherstellen (Positionen.json, Sicherungen, TraceLogs).
 public const string FolderName="Fensterpositionen wiederherstellen";
 public static string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),FolderName);
 public static string Profiles=Path.Combine(Root,"Positionen.json");
 // Früherer Ordner bis Version 2.12; wird beim ersten Start einmalig kopiert und bleibt unverändert liegen.
 public static readonly string OldRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WestMc","Fensterpositionen");
 public const string CustomKey="_custom";
 // Ausgeblendete fest eingebaute Programme (Ids), gespeichert in Positionen.json.
 public const string HiddenKey="_hidden";
 public static HashSet<string> HiddenIds(Dictionary<string,object> profiles) {
  var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  if(profiles==null||!profiles.ContainsKey(HiddenKey))return result;
  var items=profiles[HiddenKey] as System.Collections.IEnumerable;
  if(items!=null)foreach(object item in items)if(item!=null)result.Add(Convert.ToString(item));
  return result;
 }
 public static void SetHiddenIds(Dictionary<string,object> profiles,IEnumerable<string> ids) {
  var list=new List<object>();
  foreach(string id in ids)list.Add(id);
  if(list.Count==0)profiles.Remove(HiddenKey);
  else profiles[HiddenKey]=list;
 }
 // Pfad für Anzeige und Protokoll ohne Benutzernamen: C:\Users\<Name>\AppData\Local -> %LOCALAPPDATA%.
 public static string Display(string path) {
  if(String.IsNullOrEmpty(path))return path;
  string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  if(local.Length>0&&path.StartsWith(local,StringComparison.OrdinalIgnoreCase))return "%LOCALAPPDATA%"+path.Substring(local.Length);
  return path;
 }
 // Gibt eine Meldung für das Protokoll zurück, wenn Daten aus dem alten Ordner übernommen wurden, sonst null.
 public static string MigrateOldFolder() {
  try {
   if(System.IO.File.Exists(Profiles)||!Directory.Exists(OldRoot))return null;
   string oldProfiles=Path.Combine(OldRoot,"Positionen.json");
   string oldBackups=Path.Combine(OldRoot,"Sicherungen");
   if(!System.IO.File.Exists(oldProfiles)&&!Directory.Exists(oldBackups))return null;
   Directory.CreateDirectory(Root);
   int copied=0;
   if(Directory.Exists(oldBackups))copied+=CopyFolder(oldBackups,Path.Combine(Root,"Sicherungen"));
   // Positionen.json zuletzt kopieren: Sie zeigt an, dass die Übernahme abgeschlossen ist.
   if(System.IO.File.Exists(oldProfiles)){System.IO.File.Copy(oldProfiles,Profiles,false);copied++;}
   return "Einstellungen aus "+Display(OldRoot)+" übernommen ("+copied+" Datei(en)). Der alte Ordner bleibt unverändert und kann gelöscht werden.";
  } catch(Exception ex) {
   return "Alte Einstellungen konnten nicht übernommen werden: "+ex.Message;
  }
 }
 static int CopyFolder(string source,string target) {
  int count=0;
  Directory.CreateDirectory(target);
  foreach(string file in Directory.GetFiles(source)) {
   string dest=Path.Combine(target,Path.GetFileName(file));
   if(!System.IO.File.Exists(dest)){System.IO.File.Copy(file,dest);count++;}
  }
  foreach(string dir in Directory.GetDirectories(source))count+=CopyFolder(dir,Path.Combine(target,Path.GetFileName(dir)));
  return count;
 }
 // Eigene Programme (nur Fensterposition) aus den Profilen lesen.
 public static List<Entry> CustomEntries(Dictionary<string,object> profiles) {
  var result=new List<Entry>();
  if(profiles==null||!profiles.ContainsKey(CustomKey))return result;
  var items=profiles[CustomKey] as System.Collections.IEnumerable;
  if(items==null)return result;
  foreach(object item in items) {
   var d=item as Dictionary<string,object>;
   if(d==null)continue;
   string id=Field(d,"Id"),process=Field(d,"ProcessName");
   if(String.IsNullOrEmpty(id)||String.IsNullOrEmpty(process))continue;
   string name=Field(d,"Name");
   result.Add(new Entry{Id=id,Name=String.IsNullOrEmpty(name)?process:name,ProcessName=process,Exe=Field(d,"Exe"),WindowOnly=true,Custom=true,Keys=new[]{"Window"}});
  }
  return result;
 }
 public static Dictionary<string,object> CustomRecord(Entry e) {
  return new Dictionary<string,object>{{"Id",e.Id},{"Name",e.Name},{"ProcessName",e.ProcessName},{"Exe",e.Exe??""}};
 }
 // Liste der eigenen Programme in einer Profilkopie ersetzen.
 public static void SetCustomEntries(Dictionary<string,object> profiles,IEnumerable<Entry> entries) {
  var list=new List<object>();
  foreach(var e in entries)if(e.Custom)list.Add(CustomRecord(e));
  if(list.Count==0)profiles.Remove(CustomKey);
  else profiles[CustomKey]=list;
 }
 static string Field(Dictionary<string,object> d,string key){return d.ContainsKey(key)&&d[key]!=null?Convert.ToString(d[key]):null;}
 public static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength=16000000, RecursionLimit=200 }; }
 public static Dictionary<string,object> Read(string path) { return Json().Deserialize<Dictionary<string,object>>(System.IO.File.ReadAllText(path)); }
 public static void Write(string path, object value) {
  Directory.CreateDirectory(Path.GetDirectoryName(path));
  string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {
   System.IO.File.WriteAllText(temp,Json().Serialize(value),new UTF8Encoding(false));
   if(System.IO.File.Exists(path)) System.IO.File.Replace(temp,path,null);
   else System.IO.File.Move(temp,path);
  } finally { if(System.IO.File.Exists(temp)) System.IO.File.Delete(temp); }
 }
 // Startwerte.json liegt im Ordner Resources (früher direkt neben der EXE).
 public static Dictionary<string,object> Load() {
  if(System.IO.File.Exists(Profiles))return Read(Profiles);
  string start=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Resources","Startwerte.json");
  if(!System.IO.File.Exists(start))start=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Startwerte.json");
  // Ohne Startwerte (z.B. nur die EXE aus einem GitHub-Release) mit leeren Positionen beginnen.
  if(!System.IO.File.Exists(start))return new Dictionary<string,object>();
  return Read(start);
 }
 public static string BackupFile(string path,string id) {
  string dir=Path.Combine(Root,"Sicherungen",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
  Directory.CreateDirectory(dir);
  string dest=Path.Combine(dir,id+"-"+Path.GetFileName(path));
  System.IO.File.Copy(path,dest);
  return dest;
 }
 public static Dictionary<string,object> Capture(Entry e) {
  var result=new Dictionary<string,object>();
  if(e.Registry) {
   using(var key=Registry.CurrentUser.OpenSubKey(e.RegistryPath)) {
    if(key==null) throw new Exception("Registry-Schluessel fehlt.");
    foreach(string name in e.Keys) {
     object value=key.GetValue(name,null);
     if(value==null) throw new Exception("Registry-Wert fehlt: "+name);
     if(key.GetValueKind(name)==RegistryValueKind.Binary) result[name]=Convert.ToBase64String((byte[])value);
     else result[name]=value.ToString();
    }
   }
  } else {
   var data=Read(e.File);
   foreach(string key in e.Keys) {
    if(!data.ContainsKey(key)) throw new Exception("Positionsfeld fehlt: "+key);
    result[key]=data[key];
   }
  }
  return result;
 }
 public static void Restore(Entry e,Dictionary<string,object> positions) {
  foreach(string k in e.Keys) if(!positions.ContainsKey(k)) throw new Exception("Gespeicherter Positionswert fehlt: "+k);
  if(e.Registry) {
   var current=Capture(e);
   string backup=Path.Combine(Root,"Sicherungen",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+e.Id+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".json");
   Write(backup,current);
   using(var key=Registry.CurrentUser.OpenSubKey(e.RegistryPath,true)) {
    if(key==null) throw new Exception("Registry-Schluessel fehlt.");
    foreach(string name in e.Keys) key.SetValue(name,e.Id=="dumeter"?(object)Convert.FromBase64String((string)positions[name]):positions[name],e.Id=="dumeter"?RegistryValueKind.Binary:RegistryValueKind.String);
   }
  } else {
   var data=Read(e.File);
   foreach(string k in e.Keys) if(!data.ContainsKey(k)) throw new Exception("Positionsfeld fehlt: "+k);
   BackupFile(e.File,e.Id);
   foreach(string k in e.Keys) data[k]=positions[k];
   Write(e.File,data);
  }
 }
}
// Protokoll zusätzlich als Datei: %LOCALAPPDATA%\Fensterpositionen wiederherstellen\TraceLogs\Protokoll-JJJJ-MM-TT.txt
// Dateien älter als 30 Tage werden beim Start entfernt.
public static class TraceLog {
 public static bool Enabled=true;
 static readonly object sync=new object();
 public static string Folder{get{return Path.Combine(Store.Root,"TraceLogs");}}
 public static string Today{get{return Path.Combine(Folder,"Protokoll-"+DateTime.Now.ToString("yyyy-MM-dd")+".txt");}}
 public static void Write(string text) {
  if(!Enabled)return;
  try {
   lock(sync) {
    Directory.CreateDirectory(Folder);
    System.IO.File.AppendAllText(Today,DateTime.Now.ToString("HH:mm:ss")+"  "+text+Environment.NewLine,new UTF8Encoding(false));
   }
  } catch {}
 }
 public static void Cleanup(int days) {
  if(!Enabled)return;
  try {
   if(!Directory.Exists(Folder))return;
   DateTime limit=DateTime.Now.Date.AddDays(-days);
   foreach(string file in Directory.GetFiles(Folder,"Protokoll-*.txt"))
    if(System.IO.File.GetLastWriteTime(file)<limit)System.IO.File.Delete(file);
  } catch {}
 }
}
// Selbsttest: --self-test, arbeitet nur im Temp-Ordner (%TEMP%\Fensterpositionen wiederherstellen).
// Ergebnis steht in %TEMP%\Fensterpositionen wiederherstellen\Testergebnis.txt; Testdaten werden danach gelöscht.
static class SelfTest {
 public static void Run() {
  string baseDir=Path.Combine(Path.GetTempPath(),Store.FolderName);
  string test=Path.Combine(baseDir,"Test-"+Guid.NewGuid().ToString("N"));
  string resultFile=Path.Combine(baseDir,"Testergebnis.txt");
  Directory.CreateDirectory(test);Store.Root=test;Store.Profiles=Path.Combine(test,"Positionen.json");TraceLog.Enabled=false;
  bool ok=false;
  try {
   string path=Path.Combine(test,"settings.json");
   Store.Write(path,new Dictionary<string,object>{{"windowState",new Dictionary<string,object>{{"x",1},{"y",2}}},{"other","unveraendert"}});
   var e=new Entry{Id="test",File=path,Keys=new[]{"windowState"}};
   var target=new Dictionary<string,object>{{"windowState",new Dictionary<string,object>{{"x",2208},{"y",13}}}};
   Store.Restore(e,target);
   var result=Store.Read(path);
   if((string)result["other"]!="unveraendert")throw new Exception("Andere Einstellung veraendert");
   if(Store.Json().Serialize(result["windowState"])!=Store.Json().Serialize(target["windowState"]))throw new Exception("Falsche Position");
   if(Directory.GetFiles(Path.Combine(test,"Sicherungen"),"*",SearchOption.AllDirectories).Length!=1)throw new Exception("Sicherung fehlt");
   bool rejected=false;try{Store.Restore(e,new Dictionary<string,object>());}catch{rejected=true;}if(!rejected)throw new Exception("Ungueltige Werte akzeptiert");
   var profiles=new Dictionary<string,object>{{"test",target}};
   Store.SetCustomEntries(profiles,new[]{new Entry{Id="eigenes-notepad",Name="Editor",ProcessName="notepad",Exe=@"C:\Windows\notepad.exe",WindowOnly=true,Custom=true}});
   Store.Write(Store.Profiles,profiles);
   var loaded=Store.Load();
   if(!loaded.ContainsKey("test"))throw new Exception("Profil nicht gespeichert");
   var custom=Store.CustomEntries(loaded);
   if(custom.Count!=1||custom[0].ProcessName!="notepad"||!custom[0].WindowOnly)throw new Exception("Eigenes Programm nicht gespeichert");
   System.IO.File.WriteAllText(resultFile,"OK: Position wiederhergestellt, andere Einstellungen erhalten, Sicherung erstellt, unvollstaendige Werte abgewiesen, Profil und eigenes Programm gespeichert.");
   ok=true;
  } catch(Exception ex) {System.IO.File.WriteAllText(resultFile,"FEHLER: "+ex);Environment.ExitCode=1;}
  if(ok)try{Directory.Delete(test,true);}catch{}
 }
}
}
