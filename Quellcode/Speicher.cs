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
 // Ab 2.52 stehen alle Programme in Positionen.json ("_programme"); diese Angaben ersetzen die früher fest
 // eingebauten Sonderfälle: Vorgabe-Kanten, weitere Fenster mittig über dem Hauptfenster (Easy2Sync),
 // Dienst nach dem Ändern neu starten (DU Meter) und geplante Aufgabe vor dem Beenden stoppen (Task-Manager).
 public string[] DefaultEdges;
 public bool CenterOthers;
 public string RestartService, EndTask;
 // Gruppe in der Programmliste (ab 2.52), leer = ohne Gruppe.
 public string Group;
 // Fenstertitel (ab 2.52): nur das Fenster mit diesem Titel (sonst eines, das ihn enthält) gehört zum Eintrag,
 // z.B. "Steam" und "Freunde" bei steamwebhelper. Leer = Hauptfenster des Programms.
 public string WindowTitle;
}
// Ein Programm in der Vorschau beim Import (ab 2.67).
public class ImportItem {
 public string Name,Group,Target,Special;
 public bool Network;
}
public static class Store {
 // Einstellungsordner: %LOCALAPPDATA%\Fensterpositionen wiederherstellen (Positionen.json, Sicherungen, TraceLogs).
 public const string FolderName="Fensterpositionen wiederherstellen";
 public static string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),FolderName);
 public static string Profiles=Path.Combine(Root,"Positionen.json");
 // Früherer Ordner bis Version 2.12; wird beim ersten Start einmalig kopiert und bleibt unverändert liegen.
 public static readonly string OldRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WestMc","Fensterpositionen");
 // Bis 2.51: selbst hinzugefügte Programme; wird beim ersten Start in "_programme" übernommen.
 public const string CustomKey="_custom";
 // Ab 2.52: vollständige Programmliste (Reihenfolge = Reihenfolge in der Liste), gilt für alle Anordnungen.
 public const string ProgramsKey="_programme";
 // Eingeklappte Gruppen der Programmliste (Namen), gilt für alle Anordnungen.
 public const string CollapsedKey="_gruppenzu";
 // Ausgeblendete Programme (Ids), gespeichert in Positionen.json; sie bleiben in der Programmliste.
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
 // Bis 2.51 selbst hinzugefügte Programme (nur Fensterposition) aus den Profilen lesen (nur für die Übernahme).
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
   result.Add(new Entry{Id=id,Name=String.IsNullOrEmpty(name)?process:name,ProcessName=process,Exe=Field(d,"Exe"),WindowOnly=true,Keys=new[]{"Window"}});
  }
  return result;
 }
 // ---------------------------------------------------------------- Programmliste (ab 2.52)
 // Ein Programm in Positionen.json (bzw. in exportierten Einstellungen):
 //   Id, Name, ProcessName, AltProcessName, Exe, Args
 //   File + Keys              Position steht in einer JSON-Datei des Programms (z.B. Discord)
 //   RegistryPath + Keys      Position steht in der Registry (HKEY_CURRENT_USER, z.B. DU Meter)
 //   sonst                    Fensterposition wird direkt am offenen Fenster gemessen und gesetzt
 //   DefaultEdges             Vorgabe für "Kanten je Programm" (top, bottom, left, right, hcenter, vcenter)
 //   CenterOthers, RestartService, EndTask   Sonderfälle (siehe Entry)
 //   Group                    Gruppe in der Programmliste (leer = ohne Gruppe)
 //   WindowTitle              nur das Fenster mit diesem Titel (z.B. "Freunde" bei Steam)
 // Pfade stehen mit Platzhaltern (%LOCALAPPDATA%, %APPDATA%, %ProgramFiles%, …), ohne Benutzernamen.
 public static List<Entry> Programs(Dictionary<string,object> profiles){return ProgramsFrom(profiles,ProgramsKey);}
 public static List<Entry> ProgramsFrom(Dictionary<string,object> data,string key) {
  var result=new List<Entry>();
  if(data==null||!data.ContainsKey(key))return result;
  var items=data[key] as System.Collections.IEnumerable;
  if(items==null||data[key] is string)return result;
  var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(object item in items) {
   var e=ProgramFromRecord(item as Dictionary<string,object>);
   if(e!=null&&ids.Add(e.Id))result.Add(e);
  }
  return result;
 }
 static string[] Strings(Dictionary<string,object> d,string key) {
  if(!d.ContainsKey(key)||d[key]==null)return null;
  if(d[key] is string)return new[]{(string)d[key]};
  var items=d[key] as System.Collections.IEnumerable;
  if(items==null)return null;
  var list=new List<string>();
  foreach(object item in items)if(item!=null&&Convert.ToString(item).Length>0)list.Add(Convert.ToString(item));
  return list.ToArray();
 }
 static bool Flag(Dictionary<string,object> d,string key) {
  if(!d.ContainsKey(key)||d[key]==null)return false;
  if(d[key] is bool)return (bool)d[key];
  bool value;
  return Boolean.TryParse(Convert.ToString(d[key]),out value)&&value;
 }
 // Ungültige Einträge (ohne Id oder Prozessname, Datei/Registry ohne Werte) werden übergangen.
 public static Entry ProgramFromRecord(Dictionary<string,object> d) {
  if(d==null)return null;
  string id=Field(d,"Id"),process=Field(d,"ProcessName");
  if(String.IsNullOrEmpty(id)||id.StartsWith("_")||String.IsNullOrEmpty(process))return null;
  string name=Field(d,"Name"),file=Field(d,"File"),registry=Field(d,"RegistryPath");
  var e=new Entry{Id=id,Name=String.IsNullOrEmpty(name)?process:name,ProcessName=process,AltProcessName=Empty(Field(d,"AltProcessName")),
   Exe=ExpandPath(Field(d,"Exe")),Args=Empty(Field(d,"Args")),Keys=Strings(d,"Keys"),DefaultEdges=Strings(d,"DefaultEdges")??new string[0],
   CenterOthers=Flag(d,"CenterOthers"),RestartService=Empty(Field(d,"RestartService")),EndTask=Empty(Field(d,"EndTask")),Group=Empty((Field(d,"Group")??"").Trim()),WindowTitle=Empty(Field(d,"WindowTitle"))};
  if(!String.IsNullOrEmpty(file))e.File=ExpandPath(file);
  else if(!String.IsNullOrEmpty(registry)){e.Registry=true;e.RegistryPath=registry;}
  else {e.WindowOnly=true;e.Keys=new[]{"Window"};}
  if(e.Keys==null||e.Keys.Length==0)return null;
  return e;
 }
 static string Empty(string value){return String.IsNullOrEmpty(value)?null:value;}
 public static Dictionary<string,object> ProgramRecord(Entry e) {
  var d=new Dictionary<string,object>{{"Id",e.Id},{"Name",e.Name},{"ProcessName",e.ProcessName}};
  if(!String.IsNullOrEmpty(e.Group))d["Group"]=e.Group;
  if(!String.IsNullOrEmpty(e.AltProcessName))d["AltProcessName"]=e.AltProcessName;
  d["Exe"]=CompactPath(e.Exe)??"";
  if(!String.IsNullOrEmpty(e.Args))d["Args"]=e.Args;
  if(!String.IsNullOrEmpty(e.WindowTitle))d["WindowTitle"]=e.WindowTitle;
  if(!e.WindowOnly) {
   if(e.Registry)d["RegistryPath"]=e.RegistryPath;else d["File"]=CompactPath(e.File);
   d["Keys"]=new List<object>(e.Keys);
  }
  if(e.DefaultEdges!=null&&e.DefaultEdges.Length>0)d["DefaultEdges"]=new List<object>(e.DefaultEdges);
  if(e.CenterOthers)d["CenterOthers"]=true;
  if(!String.IsNullOrEmpty(e.RestartService))d["RestartService"]=e.RestartService;
  if(!String.IsNullOrEmpty(e.EndTask))d["EndTask"]=e.EndTask;
  return d;
 }
 public static List<object> ProgramRecords(IEnumerable<Entry> entries) {
  var list=new List<object>();
  foreach(var e in entries)list.Add(ProgramRecord(e));
  return list;
 }
 // Programmliste in einer Profilkopie ersetzen.
 public static void SetPrograms(Dictionary<string,object> profiles,IEnumerable<Entry> entries){profiles[ProgramsKey]=ProgramRecords(entries);}
 // Platzhalter: längste passende Ordner zuerst, damit z.B. %LOCALAPPDATA% vor %USERPROFILE% greift.
 static List<KeyValuePair<string,string>> Folders() {
  var list=new List<KeyValuePair<string,string>>();
  Action<string,string> add=(name,path)=>{if(!String.IsNullOrEmpty(path))list.Add(new KeyValuePair<string,string>(name,path.TrimEnd('\\')));};
  add("%LOCALAPPDATA%",Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
  add("%APPDATA%",Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
  add("%ProgramFiles(x86)%",Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
  add("%ProgramFiles%",Environment.GetEnvironmentVariable("ProgramW6432")??Environment.GetEnvironmentVariable("ProgramFiles"));
  add("%WINDIR%",Environment.GetFolderPath(Environment.SpecialFolder.Windows));
  add("%USERPROFILE%",Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
  list.Sort((x,y)=>y.Value.Length.CompareTo(x.Value.Length));
  return list;
 }
 public static string CompactPath(string path) {
  if(String.IsNullOrEmpty(path))return path;
  foreach(var pair in Folders())
   if(path.StartsWith(pair.Value+"\\",StringComparison.OrdinalIgnoreCase))return pair.Key+path.Substring(pair.Value.Length);
  return path;
 }
 public static string ExpandPath(string path) {
  if(String.IsNullOrEmpty(path))return path;
  // %ProgramFiles% immer als 64-Bit-Ordner (C:\Program Files), auch wenn dieses Programm als 32-Bit-Prozess läuft.
  string programs=Environment.GetEnvironmentVariable("ProgramW6432");
  if(!String.IsNullOrEmpty(programs)&&path.StartsWith("%ProgramFiles%",StringComparison.OrdinalIgnoreCase))path=programs+path.Substring(14);
  return Environment.ExpandEnvironmentVariables(path);
 }
 // Einmalige Übernahme (ab 2.52 stehen keine Programme mehr im Programm selbst): Fehlt in Positionen.json
 // "_programme", wird die Programmliste aus Resources\Startwerte.json ("_programme", falls vorhanden) und den
 // eigenen Programmen ("_custom") gebildet und in Positionen.json geschrieben; vorher wird die Datei gesichert.
 // Ohne Positionen.json gilt Startwerte.json unverändert (mit oder ohne Programmliste).
 // Gibt eine Meldung für das Protokoll zurück, sonst null.
 public static string MigratePrograms() {
  if(!System.IO.File.Exists(Profiles))return null;
  var raw=Read(Profiles);
  if(raw.ContainsKey(ProgramsKey))return null;
  string backup=BackupFile(Profiles,"vor-Programmliste");
  var programs=ProgramsFrom(LoadStart(),ProgramsKey);
  int start=programs.Count,custom=0;
  foreach(var e in CustomEntries(raw))
   if(!programs.Any(x=>String.Equals(x.Id,e.Id,StringComparison.OrdinalIgnoreCase))){programs.Add(e);custom++;}
  SetPrograms(raw,programs);
  raw.Remove(CustomKey);
  Write(Profiles,raw);
  return "Programmliste in Positionen.json angelegt: "+programs.Count+" Programme ("+start+" aus Startwerte.json, "+custom+" selbst hinzugefügt)"+
   (start==0?" – keine Programmliste in Startwerte.json gefunden; weitere Programme über „Programm hinzufügen“ oder „Einstellungen importieren“ aufnehmen":"")+
   ". Sicherung der bisherigen Datei: "+Display(backup)+".";
 }
 // Export/Import (ab 2.52): die ganze Positionen.json – Programmliste, Positionen, Kanten, Anordnungen,
 // Navigation und Update-Einstellungen. "_export" enthält nur Angaben zur Datei und wird beim Import entfernt.
 public const string ExportKey="_export";
 public static void ExportSettings(string path) {
  var raw=LoadRaw();
  raw[ExportKey]=new Dictionary<string,object>{{"Programm",AppInfo.Product},{"Version",AppInfo.Version},{"Datum",DateTime.Now.ToString("yyyy-MM-dd HH:mm")}};
  Write(path,raw);
 }
 // Import (Globale Richtlinien, Projektstruktur › Sicherung wiederherstellen; ab 2.67): erst vollständig lesen und
 // mit Grenzen prüfen, dann Vorschau (ImportPreviewForm). Programme mit Netzwerkpfad und Sonderfälle (Dienst neu
 // starten, geplante Aufgabe beenden) nur mit eigenem Schalter; PC-bezogene Werte bleiben lokal.
 public const long ImportMaxBytes=8L*1024*1024;
 public const int ImportMaxPrograms=500,ImportMaxArrangements=100;
 // PC-bezogen, werden nie aus der Datei übernommen: Update-Schalter und letzte Prüfung ("_update"),
 // Navigation angeheftet ("_nav", Stecknadel).
 public static readonly string[] LocalKeys={"_update","_nav"};
 // Liest eine exportierte Datei (auch eine Positionen.json oder Startwerte.json) und prüft sie.
 public static Dictionary<string,object> ReadSettings(string path) {
  long size=new FileInfo(path).Length;
  if(size>ImportMaxBytes)throw new Exception("Die Datei ist zu groß ("+(size/1024/1024)+" MB, höchstens "+(ImportMaxBytes/1024/1024)+" MB).");
  Dictionary<string,object> data;
  try {data=Read(path);}
  catch(Exception ex) {throw new Exception("Die Datei ist keine gültige JSON-Datei. "+ex.Message);}
  if(data==null)throw new Exception("Die Datei ist leer.");
  data.Remove(ExportKey);
  bool known=data.ContainsKey(ProgramsKey)||data.ContainsKey(CustomKey)||data.ContainsKey(ArrangementsKey)||data.Values.Any(x=>x is Dictionary<string,object>);
  if(!known)throw new Exception("Die Datei enthält keine Einstellungen von "+AppInfo.Product+".");
  foreach(string key in new[]{ProgramsKey,CustomKey}) {
   var items=data.ContainsKey(key)?data[key] as System.Collections.ICollection:null;
   if(items!=null&&items.Count>ImportMaxPrograms)throw new Exception("Die Datei enthält zu viele Programme ("+items.Count+", höchstens "+ImportMaxPrograms+").");
  }
  if(data.ContainsKey(ArrangementsKey)&&!(data[ArrangementsKey] is Dictionary<string,object>))throw new Exception("Die Anordnungen in der Datei sind ungültig.");
  int arrangements=Arrangements(data).Count;
  if(arrangements>ImportMaxArrangements)throw new Exception("Die Datei enthält zu viele Anordnungen ("+arrangements+", höchstens "+ImportMaxArrangements+").");
  return data;
 }
 // Liste der Programme, die die Datei mitbringt (ab 2.52 "_programme", bis 2.51 "_custom").
 static string ImportListKey(Dictionary<string,object> data){return data.ContainsKey(ProgramsKey)?ProgramsKey:CustomKey;}
 public static List<ImportItem> AnalyzeImport(Dictionary<string,object> data) {
  var result=new List<ImportItem>();
  foreach(var e in ProgramsFrom(data,ImportListKey(data))) {
   string target=CompactPath(e.Exe)??"";
   if(!String.IsNullOrEmpty(e.Args))target+=(target.Length>0?" ":"")+e.Args;
   if(e.File!=null)target+=(target.Length>0?" · ":"")+"Datei "+CompactPath(e.File);
   else if(e.Registry)target+=(target.Length>0?" · ":"")+"Registry HKCU\\"+e.RegistryPath;
   var specials=new List<string>();
   if(e.RestartService!=null)specials.Add("Dienst „"+e.RestartService+"“ neu starten");
   if(e.EndTask!=null)specials.Add("geplante Aufgabe „"+e.EndTask+"“ beenden");
   result.Add(new ImportItem{Name=e.Name,Group=e.Group,Target=target.Length>0?target:e.ProcessName+".exe",Network=UsesNetwork(e),Special=specials.Count>0?String.Join(", ",specials):null});
  }
  return result;
 }
 // Endgültige Einstellungen aus der Datei: Netzwerk-Programme bzw. Sonderfälle nur mit Zustimmung, lokale Werte
 // (LocalKeys) aus der bisherigen Positionen.json. Ungültige Programmeinträge fallen weg.
 public static Dictionary<string,object> PrepareImport(Dictionary<string,object> data,bool network,bool special) {
  var copy=Json().Deserialize<Dictionary<string,object>>(Json().Serialize(data));
  foreach(string key in new[]{ProgramsKey,CustomKey}) {
   if(!copy.ContainsKey(key))continue;
   var items=copy[key] as System.Collections.IEnumerable;
   var kept=new List<object>();
   if(items!=null&&!(copy[key] is string))
    foreach(object item in items) {
     var record=item as Dictionary<string,object>;
     var e=ProgramFromRecord(record);
     if(e==null)continue;
     if(!network&&UsesNetwork(e))continue;
     if(!special){record.Remove("RestartService");record.Remove("EndTask");}
     kept.Add(record);
    }
   copy[key]=kept;
  }
  foreach(string key in LocalKeys)copy.Remove(key);
  var current=System.IO.File.Exists(Profiles)?Read(Profiles):null;
  if(current!=null)foreach(string key in LocalKeys)if(current.ContainsKey(key))copy[key]=current[key];
  return copy;
 }
 // Netzwerkpfade (Globale Richtlinien, Sicherung wiederherstellen): nach ExpandEnvironmentVariables \\Server\…,
 // \\?\UNC\…, //Server/…, file://Server/… und verbundene Netzlaufwerke (DriveType Network, ohne den Server anzusprechen).
 // Gerätepfade: \\?\X:\ wird auf X:\ gekürzt, alle anderen gelten als Netzwerk.
 public static bool IsNetworkPath(string path) {
  if(String.IsNullOrEmpty(path))return false;
  string p=Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')).Replace('/','\\');
  if(p.StartsWith("file:",StringComparison.OrdinalIgnoreCase)) {
   string rest=p.Substring(5).TrimStart('\\');
   if(rest.Length>=2&&Char.IsLetter(rest[0])&&rest[1]==':')p=rest;else return true;
  }
  if(p.StartsWith(@"\\?\",StringComparison.Ordinal)) {
   string rest=p.Substring(4);
   if(rest.Length>=2&&Char.IsLetter(rest[0])&&rest[1]==':')p=rest;else return true;
  }
  if(p.StartsWith(@"\\",StringComparison.Ordinal))return true;
  if(p.Length>=2&&Char.IsLetter(p[0])&&p[1]==':') {
   try {return new DriveInfo(p.Substring(0,1)).DriveType==DriveType.Network;}
   catch {return false;}
  }
  return false;
 }
 // Programm, Positionsdatei oder ein Pfad in den Parametern liegt im Netzwerk.
 public static bool UsesNetwork(Entry e) {
  if(IsNetworkPath(e.Exe)||IsNetworkPath(e.File))return true;
  if(String.IsNullOrEmpty(e.Args))return false;
  foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(e.Args,"\"([^\"]*)\"|(\\S+)")) {
   string token=m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value;
   if(IsNetworkPath(token))return true;
   int equals=token.IndexOf('=');
   if(equals>=0&&IsNetworkPath(token.Substring(equals+1)))return true;
  }
  return false;
 }
 // Ersetzt Positionen.json (Globale Richtlinien): erst vollständig in eine Hilfsdatei schreiben und zurücklesen,
 // dann den bisherigen Stand sichern (scheitert das, wird nichts ersetzt), dann austauschen; bei einem Fehler
 // danach den alten Stand zurückholen. Bei älteren Dateien wird die Programmliste angelegt.
 // Gibt den Pfad der Sicherung zurück (null, wenn es noch keine Positionen.json gab).
 public static string ReplaceSettings(Dictionary<string,object> data) {
  Directory.CreateDirectory(Root);
  string temp=Profiles+".import-"+Guid.NewGuid().ToString("N")+".tmp",backup=null;
  try {
   System.IO.File.WriteAllText(temp,Json().Serialize(data),new UTF8Encoding(false));
   if(Read(temp)==null)throw new Exception("Die Hilfsdatei ist nicht lesbar.");
   if(System.IO.File.Exists(Profiles))backup=BackupFile(Profiles,"vor-Import");
   if(System.IO.File.Exists(Profiles))System.IO.File.Replace(temp,Profiles,null);
   else System.IO.File.Move(temp,Profiles);
  } finally {try {if(System.IO.File.Exists(temp))System.IO.File.Delete(temp);} catch {}}
  try {MigratePrograms();}
  catch {
   if(backup!=null)try {System.IO.File.Copy(backup,Profiles,true);} catch {}
   throw;
  }
  return backup;
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
 // Anordnungen (ab 2.48): Neben der Standard-Anordnung (Werte direkt in Positionen.json) lassen sich weitere
 // Anordnungen anlegen, z.B. "Arbeit" und "Spielen". Sie stehen in Positionen.json unter "_anordnungen" und enthalten
 // je Anordnung die Programmpositionen und die Kanten-Einstellungen; die aktive steht unter "_anordnung".
 // Programmliste, ausgeblendete Programme, Navigation und Update-Einstellungen gelten für alle Anordnungen.
 // Load() liefert immer die Sicht der aktiven Anordnung, SaveProfiles() schreibt sie an die richtige Stelle zurück.
 public const string ArrangementsKey="_anordnungen",ActiveKey="_anordnung",StandardName="Standard";
 static readonly string[] PerArrangement={"_edges","_edgegaps","_edgerefs","_gap","_skip","_alignment","_auswahl"};
 // Nur für diesen Programmlauf (Aufruf mit --wiederherstellen "Name"); wird nicht gespeichert.
 public static string Override;
 public static bool IsArrangementKey(string key){return !key.StartsWith("_")||Array.IndexOf(PerArrangement,key)>=0;}
 public static Dictionary<string,object> Arrangements(Dictionary<string,object> raw) {
  var found=raw!=null&&raw.ContainsKey(ArrangementsKey)?raw[ArrangementsKey] as Dictionary<string,object>:null;
  return found??new Dictionary<string,object>();
 }
 // Gespeicherter Name in der Schreibweise der Datei (Groß/klein egal), sonst null.
 public static string FindArrangement(Dictionary<string,object> raw,string name) {
  if(String.IsNullOrEmpty(name))return null;
  if(String.Equals(name,StandardName,StringComparison.OrdinalIgnoreCase))return StandardName;
  foreach(string key in Arrangements(raw).Keys)if(String.Equals(key,name,StringComparison.OrdinalIgnoreCase))return key;
  return null;
 }
 public static string ActiveName(Dictionary<string,object> raw) {
  string name=FindArrangement(raw,Override);
  if(name==null&&raw!=null&&raw.ContainsKey(ActiveKey))name=FindArrangement(raw,Convert.ToString(raw[ActiveKey]));
  return name??StandardName;
 }
 public static List<string> ArrangementNames(Dictionary<string,object> raw) {
  var names=new List<string>(Arrangements(raw).Keys);
  names.Sort(StringComparer.CurrentCultureIgnoreCase);
  names.Insert(0,StandardName);
  return names;
 }
 // Inhalt von Positionen.json, wie er in der Datei steht (ohne Umrechnung auf die aktive Anordnung).
 public static Dictionary<string,object> LoadRaw() {
  if(System.IO.File.Exists(Profiles))return Read(Profiles);
  return LoadStart();
 }
 // Sicht der aktiven Anordnung: globale Einstellungen + Positionen und Kanten der Anordnung.
 public static Dictionary<string,object> Load() {
  var raw=LoadRaw();
  string active=ActiveName(raw);
  if(active==StandardName)return raw;
  var view=new Dictionary<string,object>();
  foreach(var pair in raw)if(!IsArrangementKey(pair.Key))view[pair.Key]=pair.Value;
  var data=Arrangements(raw)[active] as Dictionary<string,object>;
  if(data!=null)foreach(var pair in data)if(IsArrangementKey(pair.Key))view[pair.Key]=pair.Value;
  return view;
 }
 // Sicht der aktiven Anordnung zurückschreiben (statt Write(Profiles,…)): Werte der Anordnung landen unter
 // "_anordnungen", globale Einstellungen direkt in Positionen.json, die anderen Anordnungen bleiben unverändert.
 public static void SaveProfiles(Dictionary<string,object> view) {
  var raw=System.IO.File.Exists(Profiles)?Read(Profiles):new Dictionary<string,object>();
  string active=ActiveName(raw);
  var output=new Dictionary<string,object>();
  if(active==StandardName) {
   foreach(var pair in view)if(pair.Key!=ArrangementsKey&&pair.Key!=ActiveKey)output[pair.Key]=pair.Value;
  } else {
   foreach(var pair in raw)if(IsArrangementKey(pair.Key))output[pair.Key]=pair.Value;
   foreach(var pair in view)if(!IsArrangementKey(pair.Key)&&pair.Key!=ArrangementsKey&&pair.Key!=ActiveKey)output[pair.Key]=pair.Value;
   var arrangements=new Dictionary<string,object>(Arrangements(raw));
   var data=new Dictionary<string,object>();
   foreach(var pair in view)if(IsArrangementKey(pair.Key))data[pair.Key]=pair.Value;
   arrangements[active]=data;
   output[ArrangementsKey]=arrangements;
  }
  if(active==StandardName&&raw.ContainsKey(ArrangementsKey))output[ArrangementsKey]=raw[ArrangementsKey];
  if(raw.ContainsKey(ActiveKey))output[ActiveKey]=raw[ActiveKey];
  Write(Profiles,output);
 }
 public static void SetActiveArrangement(string name) {
  var raw=LoadRaw();
  if(name==StandardName)raw.Remove(ActiveKey);else raw[ActiveKey]=name;
  Write(Profiles,raw);
 }
 // Neue Anordnung als Kopie der Werte aus view (Positionen und Kanten); wird gleich aktiv.
 public static void CreateArrangement(string name,Dictionary<string,object> view) {
  var raw=LoadRaw();
  var arrangements=new Dictionary<string,object>(Arrangements(raw));
  var data=new Dictionary<string,object>();
  foreach(var pair in view)if(IsArrangementKey(pair.Key))data[pair.Key]=pair.Value;
  arrangements[name]=Json().Deserialize<Dictionary<string,object>>(Json().Serialize(data));
  raw[ArrangementsKey]=arrangements;
  raw[ActiveKey]=name;
  Write(Profiles,raw);
 }
 public static void RenameArrangement(string oldName,string newName) {
  var raw=LoadRaw();
  var arrangements=new Dictionary<string,object>(Arrangements(raw));
  if(!arrangements.ContainsKey(oldName))return;
  object data=arrangements[oldName];
  arrangements.Remove(oldName);
  arrangements[newName]=data;
  raw[ArrangementsKey]=arrangements;
  if(raw.ContainsKey(ActiveKey)&&Convert.ToString(raw[ActiveKey])==oldName)raw[ActiveKey]=newName;
  Write(Profiles,raw);
 }
 public static void DeleteArrangement(string name) {
  var raw=LoadRaw();
  var arrangements=new Dictionary<string,object>(Arrangements(raw));
  arrangements.Remove(name);
  if(arrangements.Count==0)raw.Remove(ArrangementsKey);else raw[ArrangementsKey]=arrangements;
  if(raw.ContainsKey(ActiveKey)&&Convert.ToString(raw[ActiveKey])==name)raw.Remove(ActiveKey);
  Write(Profiles,raw);
 }
 // Startwerte.json liegt im Ordner Resources (früher direkt neben der EXE). Sie enthält eigene Startpositionen
 // und ab 2.52 die Programmliste ("_programme") für neue Einstellungen; StartOverride nur für den Selbsttest.
 public static string StartOverride;
 static Dictionary<string,object> LoadStart() {
  string start=StartOverride??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Resources","Startwerte.json");
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
    // Werteart wie vorhanden: Binärwerte stehen als Base64 in Positionen.json (z.B. DU Meter), Zahlen als Text.
    foreach(string name in e.Keys) {
     var kind=key.GetValueKind(name);
     string text=Convert.ToString(positions[name]);
     if(kind==RegistryValueKind.Binary)key.SetValue(name,Convert.FromBase64String(text),kind);
     else if(kind==RegistryValueKind.DWord)key.SetValue(name,Int32.Parse(text),kind);
     else if(kind==RegistryValueKind.QWord)key.SetValue(name,Int64.Parse(text),kind);
     else key.SetValue(name,text,RegistryValueKind.String);
    }
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
   // Format bis 2.51: eigene Programme unter "_custom"; die Übernahme bildet die Programmliste aus
   // Startwerte.json (hier eine Testdatei) und den eigenen Programmen.
   profiles[Store.CustomKey]=new List<object>{new Dictionary<string,object>{{"Id","eigenes-notepad"},{"Name","Editor"},{"ProcessName","notepad"},{"Exe",@"C:\Windows\notepad.exe"}}};
   Store.Write(Store.Profiles,profiles);
   Store.StartOverride=Path.Combine(test,"Startwerte.json");
   Store.Write(Store.StartOverride,new Dictionary<string,object>{{Store.ProgramsKey,new List<object>{
    new Dictionary<string,object>{{"Id","dumeter"},{"Name","DU Meter"},{"ProcessName","DUMeter"},{"Exe",@"%ProgramFiles(x86)%\DU Meter\DUMeter.exe"},
     {"RegistryPath",@"Software\Hagel\DU Meter\Window"},{"Keys",new[]{"WindowPosition"}},{"DefaultEdges",new[]{"top","right"}},{"RestartService","DUMeterSvc"}},
    new Dictionary<string,object>{{"Id","discord"},{"Name","Discord"},{"ProcessName","Discord"},{"Exe",@"%LOCALAPPDATA%\Discord\Update.exe"},{"Args","--processStart Discord.exe"},
     {"File",@"%APPDATA%\discord\settings.json"},{"Keys",new[]{"WINDOW_BOUNDS","IS_MAXIMIZED","IS_MINIMIZED"}}},
    new Dictionary<string,object>{{"Id","easy2sync"},{"Name","Easy2Sync"},{"ProcessName","Easy2Sync"},{"CenterOthers",true}},
    new Dictionary<string,object>{{"Name","ungültig ohne Id"},{"ProcessName","x"}}}}});
   if(Store.MigratePrograms()==null)throw new Exception("Programmliste nicht übernommen");
   if(Store.MigratePrograms()!=null)throw new Exception("Programmliste zweimal übernommen");
   var loaded=Store.Load();
   if(!loaded.ContainsKey("test"))throw new Exception("Profil nicht gespeichert");
   if(loaded.ContainsKey(Store.CustomKey))throw new Exception("Alte Liste _custom nicht entfernt");
   var programs=Store.Programs(loaded);
   int expected=4;
   if(programs.Count!=expected)throw new Exception("Programmliste unvollständig: "+programs.Count);
   var custom=programs[programs.Count-1];
   if(custom.ProcessName!="notepad"||!custom.WindowOnly)throw new Exception("Eigenes Programm nicht übernommen");
   var dumeter=programs.First(x=>x.Id=="dumeter");
   if(!dumeter.Registry||dumeter.RestartService!="DUMeterSvc"||!dumeter.DefaultEdges.Contains("right"))throw new Exception("Sonderfälle DU Meter fehlen");
   var discord=programs.First(x=>x.Id=="discord");
   if(discord.File.Contains("%")||!Store.Json().Serialize(Store.ProgramRecord(discord)).Contains("%APPDATA%"))throw new Exception("Platzhalter in Pfaden falsch");
   if(!programs.First(x=>x.Id=="easy2sync").CenterOthers)throw new Exception("Sonderfall Easy2Sync fehlt");
   // Export und Import ergeben dieselben Einstellungen.
   string export=Path.Combine(test,"Einstellungen.json");
   Store.ExportSettings(export);
   var imported=Store.ReadSettings(export);
   if(imported.ContainsKey(Store.ExportKey))throw new Exception("Exportangaben nicht entfernt");
   Store.ReplaceSettings(Store.PrepareImport(imported,true,true));
   if(Store.Json().Serialize(Store.LoadRaw())!=Store.Json().Serialize(imported))throw new Exception("Export/Import unterschiedlich");
   // Import-Regeln (ab 2.67): Netzwerkpfade und Sonderfälle nur mit Schalter, lokale Werte bleiben.
   foreach(string sample in new[]{@"\\server\x.exe",@"//server/x.exe",@"file://server/x.exe",@"\\?\UNC\server\x.exe",@"\\.\pipe\x"})
    if(!Store.IsNetworkPath(sample))throw new Exception("Netzwerkpfad nicht erkannt: "+sample);
   foreach(string sample in new[]{@"C:\Windows\notepad.exe",@"%WINDIR%\notepad.exe",@"\\?\C:\Windows\notepad.exe",@"file:///C:/Windows/notepad.exe",""})
    if(Store.IsNetworkPath(sample))throw new Exception("Lokaler Pfad als Netzwerk erkannt: "+sample);
   var foreign=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(imported));
   var list=(System.Collections.IList)foreign[Store.ProgramsKey];
   list.Add(new Dictionary<string,object>{{"Id","netz"},{"Name","Netz"},{"ProcessName","netz"},{"Exe",@"\\server\netz.exe"}});
   list.Add(new Dictionary<string,object>{{"Id","parameter"},{"Name","Parameter"},{"ProcessName","p"},{"Exe",@"C:\p.exe"},{"Args","--config=\"//server/p.ini\""}});
   foreign["_update"]=new Dictionary<string,object>{{"auto",false}};
   foreign["_nav"]=new Dictionary<string,object>{{"pinned",false}};
   var local=Store.LoadRaw();
   local["_update"]=new Dictionary<string,object>{{"auto",true}};
   Store.Write(Store.Profiles,local);
   var preview=Store.AnalyzeImport(foreign);
   if(preview.Count(x=>x.Network)!=2||preview.Count(x=>x.Special!=null)!=1)throw new Exception("Vorschau erkennt Netzwerkpfade/Sonderfälle nicht");
   var safe=Store.PrepareImport(foreign,false,false);
   var safePrograms=Store.ProgramsFrom(safe,Store.ProgramsKey);
   if(safePrograms.Count!=expected||safePrograms.Any(x=>x.RestartService!=null||x.EndTask!=null))throw new Exception("Netzwerk/Sonderfälle ohne Zustimmung übernommen");
   if(Store.ProgramsFrom(Store.PrepareImport(foreign,true,true),Store.ProgramsKey).Count!=expected+2)throw new Exception("Netzwerk-Programme trotz Zustimmung entfernt");
   if(safe.ContainsKey("_nav")||!Convert.ToBoolean(((Dictionary<string,object>)safe["_update"])["auto"]))throw new Exception("Lokale Werte aus der Datei übernommen");
   Store.ReplaceSettings(safe);
   // Anordnungen: eigene Positionen je Anordnung, die Programmliste gilt für alle.
   Store.CreateArrangement("Spielen",loaded);
   var play=Store.Load();
   play["test"]=new Dictionary<string,object>{{"windowState",new Dictionary<string,object>{{"x",5},{"y",6}}}};
   play["_gap"]=30;
   Store.SaveProfiles(play);
   Store.SetActiveArrangement(Store.StandardName);
   var standard=Store.Load();
   if(Store.Json().Serialize(standard["test"])!=Store.Json().Serialize(target)||standard.ContainsKey("_gap"))throw new Exception("Anordnung Standard veraendert");
   if(Store.Programs(standard).Count!=expected)throw new Exception("Programmliste fehlt in Standard");
   Store.Override="spielen";
   var again=Store.Load();
   Store.Override=null;
   if(Convert.ToInt32(again["_gap"])!=30||Store.Programs(again).Count!=expected)throw new Exception("Anordnung Spielen nicht gespeichert");
   System.IO.File.WriteAllText(resultFile,"OK: Position wiederhergestellt, andere Einstellungen erhalten, Sicherung erstellt, unvollstaendige Werte abgewiesen, Profil, Programmliste (Übernahme), Export/Import (mit Import-Regeln) und Anordnungen gespeichert.");
   ok=true;
  } catch(Exception ex) {System.IO.File.WriteAllText(resultFile,"FEHLER: "+ex);Environment.ExitCode=1;}
  if(ok)try{Directory.Delete(test,true);}catch{}
 }
}
}
