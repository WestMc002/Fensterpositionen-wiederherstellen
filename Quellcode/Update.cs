// Fensterpositionen wiederherstellen – Update-Prüfung über die GitHub-Releases und Selbst-Update
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace Fensterpositionen {

// Neueste Veröffentlichung auf GitHub.
public class ReleaseInfo {
 public string Version,PageUrl,ZipUrl;
}

// Fragt nur die öffentliche GitHub-Schnittstelle ab (keine Anmeldung, es werden keine Daten übertragen).
public static class Updater {
 public const string Repository="WestMc002/Fensterpositionen-wiederherstellen";
 public const string ReleasesPage="https://github.com/"+Repository+"/releases";
 // Arbeitsordner für Download und Hilfsskript (%TEMP%\<Programm>-Update).
 public static string Folder{get{return Path.Combine(Path.GetTempPath(),"Fensterpositionen wiederherstellen-Update");}}
 public static string LogFile{get{return Path.Combine(Folder,"Update.log");}}

 static void PrepareTls() {
  // .NET 4: TLS 1.2 (3072) für github.com einschalten.
  try {ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;} catch {}
 }
 static string UserAgent{get{return "Fensterpositionen-wiederherstellen/"+AppInfo.Version;}}

 public static ReleaseInfo GetLatest() {
  PrepareTls();
  var request=(HttpWebRequest)WebRequest.Create("https://api.github.com/repos/"+Repository+"/releases/latest");
  request.UserAgent=UserAgent;
  request.Accept="application/vnd.github+json";
  request.Timeout=15000;request.ReadWriteTimeout=15000;
  string json;
  try {
   using(var response=(HttpWebResponse)request.GetResponse())
   using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)) json=reader.ReadToEnd();
  } catch(WebException ex) {
   var http=ex.Response as HttpWebResponse;
   if(http!=null&&http.StatusCode==HttpStatusCode.NotFound)throw new Exception("Auf GitHub ist noch keine Version veröffentlicht.");
   if(http!=null&&(int)http.StatusCode==403)throw new Exception("GitHub hat die Anfrage abgelehnt (zu viele Anfragen). Später erneut versuchen.");
   throw new Exception("GitHub nicht erreichbar: "+ex.Message);
  }
  var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
  var info=new ReleaseInfo{PageUrl=ReleasesPage};
  object value;
  if(data.TryGetValue("tag_name",out value)&&value!=null)info.Version=Convert.ToString(value).TrimStart('v','V');
  if(data.TryGetValue("html_url",out value)&&value!=null)info.PageUrl=Convert.ToString(value);
  // Listen kommen je nach Serializer als ArrayList oder object[] an.
  if(data.TryGetValue("assets",out value)&&value is System.Collections.IEnumerable&&!(value is string)) {
   foreach(var item in (System.Collections.IEnumerable)value) {
    var asset=item as Dictionary<string,object>;
    if(asset==null||!asset.ContainsKey("name")||!asset.ContainsKey("browser_download_url"))continue;
    if(Convert.ToString(asset["name"]).EndsWith(".zip",StringComparison.OrdinalIgnoreCase)){info.ZipUrl=Convert.ToString(asset["browser_download_url"]);break;}
   }
  }
  if(String.IsNullOrEmpty(info.Version))throw new Exception("Antwort von GitHub ohne Versionsnummer.");
  return info;
 }

 // true, wenn remote neuer ist als die laufende Version.
 public static bool IsNewer(string remote) {
  Version a,b;
  if(!System.Version.TryParse(remote,out a)||!System.Version.TryParse(AppInfo.Version,out b))return false;
  return a>b;
 }

 // Im Projektordner (mit Erstellen.bat) wird nicht automatisch ersetzt: dort entsteht die EXE beim Bauen.
 public static string InstallBlocker() {
  string dir=AppDomain.CurrentDomain.BaseDirectory;
  if(File.Exists(Path.Combine(dir,"Erstellen.bat")))return "Im Projektordner (mit Erstellen.bat) wird nicht automatisch aktualisiert – dort entsteht die EXE beim Erstellen.";
  return null;
 }
 static bool CanWrite(string dir) {
  try {
   string probe=Path.Combine(dir,"~update-test.tmp");
   File.WriteAllText(probe,"");File.Delete(probe);
   return true;
  } catch {return false;}
 }

 // ZIP herunterladen, Hilfsskript schreiben und starten. Das Skript wartet, bis dieses Programm beendet ist,
 // ersetzt EXE und Anleitung und startet das Programm neu. Danach muss der Aufrufer das Programm beenden.
 public static async Task Install(ReleaseInfo release,Action<int> progress) {
  if(String.IsNullOrEmpty(release.ZipUrl))throw new Exception("Im Release ist keine ZIP-Datei angehängt.");
  string blocker=InstallBlocker();
  if(blocker!=null)throw new Exception(blocker);
  PrepareTls();
  Directory.CreateDirectory(Folder);
  string zip=Path.Combine(Folder,"Fensterpositionen-wiederherstellen-"+release.Version+".zip");
  using(var client=new WebClient()) {
   client.Headers[HttpRequestHeader.UserAgent]=UserAgent;
   if(progress!=null)client.DownloadProgressChanged+=(s,e)=>progress(e.ProgressPercentage);
   await client.DownloadFileTaskAsync(new Uri(release.ZipUrl),zip);
  }
  if(new FileInfo(zip).Length<1024)throw new Exception("Die heruntergeladene Datei ist unvollständig.");
  string script=Path.Combine(Folder,"Update.ps1");
  File.WriteAllText(script,Script,new UTF8Encoding(true));
  try {File.Delete(LogFile);} catch {}
  string exe=Application.ExecutablePath;
  string target=Path.GetDirectoryName(exe);
  var start=new ProcessStartInfo("powershell.exe",
   "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \""+script+"\" -ProcessId "+Process.GetCurrentProcess().Id+
   " -Zip \""+zip+"\" -Exe \""+exe+"\" -Version \""+release.Version+"\""){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden};
  // Programmordner ohne Schreibrechte (z.B. unter Programme): Hilfsskript mit Administratorrechten starten.
  if(!CanWrite(target))start.Verb="runas";
  try {Process.Start(start);}
  catch(System.ComponentModel.Win32Exception ex) {
   if(ex.NativeErrorCode==1223)throw new Exception("Update abgebrochen (Administratorrechte nicht erteilt).");
   throw;
  }
 }

 // Ergebnis des letzten Updates (nach dem Neustart mit --nach-update) für das Protokoll.
 public static string LastResult() {
  try {return File.Exists(LogFile)?File.ReadAllText(LogFile,Encoding.UTF8).Trim():null;} catch {return null;}
 }

 const string Script=@"# Fensterpositionen wiederherstellen – Update (wird vom Programm erzeugt)
param([int]$ProcessId,[string]$Zip,[string]$Exe,[string]$Version)
$ErrorActionPreference = 'Stop'
$log = Join-Path $PSScriptRoot 'Update.log'
$name = Split-Path $Exe -Leaf
$target = Split-Path $Exe -Parent
try {
  try { Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction SilentlyContinue } catch {}
  Start-Sleep -Milliseconds 500
  $dir = Join-Path $PSScriptRoot 'Neu'
  if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
  Expand-Archive -Path $Zip -DestinationPath $dir -Force
  $new = Get-ChildItem $dir -Recurse -File | Where-Object { $_.Name -eq $name } | Select-Object -First 1
  if (-not $new) { throw ('Im ZIP fehlt ' + $name) }
  $old = $Exe + '.alt'
  Copy-Item $Exe $old -Force
  try {
    Copy-Item $new.FullName $Exe -Force
    Get-ChildItem $new.DirectoryName -File | Where-Object { $_.Name -ne $name } | Copy-Item -Destination $target -Force
  } catch {
    Copy-Item $old $Exe -Force
    throw
  }
  Remove-Item $old -Force
  Remove-Item $dir -Recurse -Force
  Remove-Item $Zip -Force
  ('OK ' + $Version) | Out-File $log -Encoding utf8
} catch {
  ('FEHLER ' + $_.Exception.Message) | Out-File $log -Encoding utf8
}
Start-Process -FilePath $Exe -ArgumentList '--nach-update'
";
}

// Hinweisleiste oben im Hauptfenster: neue Version mit "Jetzt aktualisieren", "Release-Seite" und "Später".
public class UpdateBar:Control {
 public readonly ModernButton Install=new ModernButton("Jetzt aktualisieren",ButtonKind.Primary,"");
 public readonly ModernButton Page=new ModernButton("Release-Seite",ButtonKind.Secondary,"");
 public readonly ModernButton Later=new ModernButton("Später",ButtonKind.Ghost,null);
 public UpdateBar() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
  BackColor=Ui.Bg;
  foreach(var b in new[]{Install,Page,Later}){b.Margin=new Padding(0);Controls.Add(b);}
 }
 protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
 protected override void OnLayout(LayoutEventArgs e) {
  base.OnLayout(e);
  int x=Width-Ui.Px(10);
  foreach(var b in new[]{Later,Page,Install}) {
   x-=b.Width;
   b.Location=new Point(x,(Height-b.Height)/2);
   x-=Ui.Px(8);
  }
 }
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  g.Clear(Ui.Bg);
  g.SmoothingMode=SmoothingMode.AntiAlias;
  var r=new Rectangle(0,0,Width-1,Height-1);
  using(var path=Ui.Round(r,Ui.Px(8)))
  using(var brush=new SolidBrush(Ui.Blend(Ui.Bg,Ui.Accent,0.14f)))
  using(var pen=new Pen(Ui.Blend(Ui.Bg,Ui.Accent,0.45f))) {g.FillPath(brush,path);g.DrawPath(pen,path);}
  int x=Ui.Px(16);
  if(Ui.IconFont!=null) {
   TextRenderer.DrawText(g,"",Ui.IconFont,new Rectangle(x,0,Ui.Px(20),Height),Ui.Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
   x+=Ui.Px(34);
  }
  int right=Install.Left-Ui.Px(12);
  TextRenderer.DrawText(g,Text,Ui.UiSemibold,new Rectangle(x,0,Math.Max(10,right-x),Height),Ui.TextColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
 }
}
}
