// Fensterpositionen wiederherstellen – Fenster messen, ausrichten und verschieben (sichtbare Kante über DWM)
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
public static class VisibleAlignment {
 [StructLayout(LayoutKind.Sequential)] public struct RECT {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO {public int Size;public RECT Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
 [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
 [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int cmd);
 // Fehlertext für SetWindowPos; Fehler 5 heißt meist: Zielprogramm läuft mit Administratorrechten.
 static string MoveError(int error,string text) {
  if(error==5)return text+" Zugriff verweigert: Das Programm läuft vermutlich mit Administratorrechten. Fensterpositionen dann ebenfalls als Administrator starten (Schaltfläche \"Als Administrator\" oder Rechtsklick > Als Administrator ausführen).";
  if(error==0)return text;
  return text+" (Windows-Fehler "+error+": "+new System.ComponentModel.Win32Exception(error).Message+")";
 }
 public static IntPtr FindMain(Entry e) {
  foreach(string name in new[]{e.ProcessName,e.AltProcessName}) {
   if(String.IsNullOrEmpty(name))continue;
   foreach(var process in Process.GetProcessesByName(name))using(process){
    if(process.MainWindowHandle!=IntPtr.Zero)return process.MainWindowHandle;
   }
  }
  return FindLargestWindow(e);
 }
 // Ersatz, wenn Windows kein Hauptfenster meldet (z.B. DU Meter: sein sichtbares Fenster gehört einem
 // versteckten Hauptfenster oder ist ein Werkzeugfenster): größtes sichtbares Fenster des Programms.
 static IntPtr FindLargestWindow(Entry e) {
  var ids=new HashSet<uint>();
  foreach(string name in new[]{e.ProcessName,e.AltProcessName}) {
   if(String.IsNullOrEmpty(name))continue;
   foreach(var process in Process.GetProcessesByName(name))using(process)ids.Add((uint)process.Id);
  }
  if(ids.Count==0)return IntPtr.Zero;
  IntPtr best=IntPtr.Zero;long bestArea=0;
  EnumWindows((hwnd,lParam)=>{
   uint id;
   GetWindowThreadProcessId(hwnd,out id);
   if(!ids.Contains(id)||!IsWindowVisible(hwnd))return true;
   RECT r;
   if(!GetWindowRect(hwnd,out r))return true;
   long area=(long)Math.Max(0,r.Right-r.Left)*Math.Max(0,r.Bottom-r.Top);
   // Winzige Hilfsfenster (z.B. Infobereich) zählen nicht.
   if(r.Right-r.Left<40||r.Bottom-r.Top<20)return true;
   if(area>bestArea){bestArea=area;best=hwnd;}
   return true;
  },IntPtr.Zero);
  return best;
 }
 public static string AlignEntry(Entry e,int gap,string edges){return AlignHandle(FindMain(e),gap,edges);}
 delegate bool EnumWindowsProc(IntPtr hwnd,IntPtr lParam);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback,IntPtr lParam);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd,int index);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int max);
 // Weitere sichtbare Fenster mit Titelleiste desselben Programms (z.B. das Easy2Sync-Synchronisationsfenster).
 static List<IntPtr> OtherWindows(Entry e,IntPtr main) {
  var ids=new HashSet<uint>();
  foreach(string name in new[]{e.ProcessName,e.AltProcessName}) {
   if(String.IsNullOrEmpty(name))continue;
   foreach(var process in Process.GetProcessesByName(name))using(process)ids.Add((uint)process.Id);
  }
  var result=new List<IntPtr>();
  EnumWindows((hwnd,lParam)=>{
   uint id;
   GetWindowThreadProcessId(hwnd,out id);
   if(hwnd!=main&&ids.Contains(id)&&IsWindowVisible(hwnd)&&!IsIconic(hwnd)) {
    int style=GetWindowLong(hwnd,-16),exStyle=GetWindowLong(hwnd,-20);
    if((style&0x00C00000)==0x00C00000&&(exStyle&0x80)==0)result.Add(hwnd);
   }
   return true;
  },IntPtr.Zero);
  return result;
 }
 // Weitere Fenster (z.B. Easy2Sync "Synchronisation: ...") waagerecht mittig über dem Hauptfenster setzen;
 // Größe und senkrechte Lage bleiben. Ragt das Fenster dann aus dem Arbeitsbereich, wird es hineingeschoben.
 public static List<string> CenterOthersOnMain(Entry e) {
  var lines=new List<string>();
  IntPtr main=FindMain(e);
  if(main==IntPtr.Zero)return lines;
  IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
  try {
   RECT mainRect;
   if(DwmGetWindowAttribute(main,9,out mainRect,16)!=0)return lines;
   foreach(IntPtr hwnd in OtherWindows(e,main)) {
    var title=new StringBuilder(256);
    GetWindowText(hwnd,title,title.Capacity);
    string name=title.Length>0?title.ToString():"Weiteres Fenster";
    try {
     RECT outer,visible;
     if(!GetWindowRect(hwnd,out outer)||DwmGetWindowAttribute(hwnd,9,out visible,16)!=0)throw new Exception("Fensterkante konnte nicht gemessen werden.");
     int width=visible.Right-visible.Left;
     int target=mainRect.Left+((mainRect.Right-mainRect.Left)-width)/2;
     var info=new MONITORINFO();info.Size=Marshal.SizeOf(typeof(MONITORINFO));
     if(GetMonitorInfo(MonitorFromWindow(main,2),ref info)) {
      if(target+width>info.Work.Right)target=info.Work.Right-width;
      if(target<info.Work.Left)target=info.Work.Left;
     }
     int dx=target-visible.Left;
     if(dx!=0&&!SetWindowPos(hwnd,IntPtr.Zero,outer.Left+dx,outer.Top,0,0,0x0001|0x0004|0x0010))
      throw new Exception(MoveError(Marshal.GetLastWin32Error(),"Fenster konnte nicht verschoben werden."));
     Thread.Sleep(200);
     DwmGetWindowAttribute(hwnd,9,out visible,16);
     int left=visible.Left-mainRect.Left,right=mainRect.Right-visible.Right;
     lines.Add(name+": mittig über dem Hauptfenster (links "+left+" px, rechts "+right+" px)");
    } catch(Exception ex) {lines.Add(name+": "+ex.Message);}
   }
  } finally {if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
  return lines;
 }
 public static List<string> AlignOthers(Entry e,int gap,string edges) {
  var lines=new List<string>();
  foreach(IntPtr hwnd in OtherWindows(e,FindMain(e))) {
   var title=new StringBuilder(256);
   GetWindowText(hwnd,title,title.Capacity);
   string name=title.Length>0?title.ToString():"Weiteres Fenster";
   try {lines.Add(name+": "+AlignHandle(hwnd,gap,edges));}
   catch(Exception ex) {lines.Add(name+": "+ex.Message);}
  }
  return lines;
 }
 // Fensterposition direkt messen (physische Bildschirmpixel).
 public static Dictionary<string,object> CaptureWindow(Entry e) {
  IntPtr handle=FindMain(e);
  if(handle==IntPtr.Zero)throw new Exception("Kein offenes Fenster gefunden. "+e.Name+" zuerst öffnen und an die gewünschte Stelle schieben.");
  IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
  try {
   if(IsIconic(handle)||IsZoomed(handle))throw new Exception("Fenster ist minimiert oder maximiert. Bitte zuerst normal anzeigen.");
   RECT r;
   if(!GetWindowRect(handle,out r))throw new Exception("Fensterposition konnte nicht gelesen werden.");
   return new Dictionary<string,object>{{"Left",r.Left},{"Top",r.Top},{"Width",r.Right-r.Left},{"Height",r.Bottom-r.Top}};
  } finally {if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
 }
 // Fenster an die gespeicherte Position setzen; Programm bei Bedarf starten.
 public static string RestoreWindow(Entry e,Dictionary<string,object> pos) {
  foreach(string k in new[]{"Left","Top","Width","Height"}) if(!pos.ContainsKey(k))throw new Exception("Gespeicherter Positionswert fehlt: "+k);
  int left=Convert.ToInt32(pos["Left"]),top=Convert.ToInt32(pos["Top"]),width=Convert.ToInt32(pos["Width"]),height=Convert.ToInt32(pos["Height"]);
  if(width<=0||height<=0)throw new Exception("Gespeicherte Fenstergröße ist ungültig.");
  IntPtr handle=FindMain(e);
  bool started=false;
  if(handle==IntPtr.Zero) {
   // Eigene Programme ohne bekannte Programmdatei können nicht gestartet werden.
   if(String.IsNullOrEmpty(e.Exe))throw new Exception("Kein offenes Fenster gefunden. "+e.Name+" bitte zuerst öffnen.");
   if(!System.IO.File.Exists(e.Exe))throw new FileNotFoundException("Programmdatei fehlt: "+e.Exe);
   Process.Start(new ProcessStartInfo(e.Exe,e.Args??""){WorkingDirectory=Path.GetDirectoryName(e.Exe),UseShellExecute=true});
   started=true;
   var timer=Stopwatch.StartNew();
   while(handle==IntPtr.Zero&&timer.ElapsedMilliseconds<30000){Thread.Sleep(500);handle=FindMain(e);}
   if(handle==IntPtr.Zero)throw new Exception("Programm gestartet, aber nach 30 Sekunden kein Fenster gefunden.");
   Thread.Sleep(1500);
  }
  string result=null;
  // Nach einem Start mehrfach setzen, weil das Programm seine eigene Position nachladen kann.
  int rounds=started?4:1;
  for(int i=0;i<rounds;i++) {
   if(i>0){Thread.Sleep(1500);IntPtr current=FindMain(e);if(current!=IntPtr.Zero)handle=current;}
   result=Place(handle,left,top,width,height);
  }
  return (started?"Gestartet; ":"")+result;
 }
 static string Place(IntPtr handle,int left,int top,int width,int height) {
  IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
  try {
   if(IsIconic(handle)||IsZoomed(handle)){ShowWindow(handle,9);Thread.Sleep(300);}
   if(!SetWindowPos(handle,IntPtr.Zero,left,top,width,height,0x0004|0x0010))
    throw new Exception(MoveError(Marshal.GetLastWin32Error(),"Fenster konnte nicht verschoben werden."));
   Thread.Sleep(300);
   RECT r;
   if(!GetWindowRect(handle,out r))throw new Exception("Neue Fensterposition konnte nicht geprüft werden.");
   if(Math.Abs(r.Left-left)>2||Math.Abs(r.Top-top)>2)
    throw new Exception("Fenster steht nach dem Verschieben bei "+r.Left+", "+r.Top+" statt "+left+", "+top+".");
   return "Position "+left+", "+top+" ("+(r.Right-r.Left)+" × "+(r.Bottom-r.Top)+")";
  } finally {if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
 }
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attr,out RECT rect,int size);
 public static string Align(string processName,int gap,string edges) {
  IntPtr handle=IntPtr.Zero;
  foreach(var process in Process.GetProcessesByName(processName))using(process){
   if(process.MainWindowHandle!=IntPtr.Zero){handle=process.MainWindowHandle;break;}
  }
  return AlignHandle(handle,gap,edges);
 }
 // Kanten als Text, z.B. "top bottom left=1200": "=Zahl" ist ein eigener Abstand für diese Kante
 // (Kanten je Programm), sonst gilt der allgemeine Abstand gap.
 // ">Zahl" ersetzt den Rand des Arbeitsbereichs durch eine Bezugslinie (Kante eines Nachbarprogramms),
 // z.B. "left=18>1834": linke Kante 18 px rechts von x=1834.
 static HashSet<string> ParseEdges(string edges,int gap,Dictionary<string,int> gaps,Dictionary<string,int> bounds) {
  var set=new HashSet<string>();
  foreach(string raw in (edges??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)) {
   string token=raw;
   int gt=token.IndexOf('>');
   if(gt>0){int line;if(Int32.TryParse(token.Substring(gt+1),out line))bounds[token.Substring(0,Math.Min(gt,token.IndexOf('=')>0?token.IndexOf('='):gt))]=line;token=token.Substring(0,gt);}
   string name=token;int value=gap;
   int eq=token.IndexOf('=');
   if(eq>0){name=token.Substring(0,eq);int parsed;if(Int32.TryParse(token.Substring(eq+1),out parsed))value=Math.Max(0,parsed);}
   set.Add(name);gaps[name]=value;
  }
  foreach(string name in new[]{"top","bottom","left","right"})if(!gaps.ContainsKey(name))gaps[name]=gap;
  return set;
 }
 // Sichtbarer Fensterrahmen eines Programms (physische Pixel); false, wenn kein normales Fenster offen ist.
 public static bool TryVisibleRect(Entry e,out Rectangle rect) {
  rect=Rectangle.Empty;
  IntPtr handle=FindMain(e);
  if(handle==IntPtr.Zero)return false;
  IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
  try {
   if(IsIconic(handle))return false;
   RECT visible;
   if(DwmGetWindowAttribute(handle,9,out visible,16)!=0)return false;
   rect=Rectangle.FromLTRB(visible.Left,visible.Top,visible.Right,visible.Bottom);
   return true;
  } finally {if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
 }
 // Aktuelle Abstände der sichtbaren Fensterkanten zum Arbeitsbereich (für "Messen" unter Kanten je Programm).
 public static Dictionary<string,int> MeasureGaps(Entry e) {
  IntPtr handle=FindMain(e);
  if(handle==IntPtr.Zero)throw new Exception("Kein offenes Fenster gefunden. "+e.Name+" zuerst öffnen.");
  IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
  try {
   if(IsIconic(handle)||IsZoomed(handle))throw new Exception(e.Name+" ist minimiert oder maximiert. Bitte zuerst normal anzeigen.");
   var info=new MONITORINFO();info.Size=Marshal.SizeOf(typeof(MONITORINFO));
   if(!GetMonitorInfo(MonitorFromWindow(handle,2),ref info))throw new Exception("Bildschirmfläche konnte nicht gelesen werden.");
   RECT visible;
   if(DwmGetWindowAttribute(handle,9,out visible,16)!=0)throw new Exception("Sichtbare Fensterkante konnte nicht gemessen werden.");
   return new Dictionary<string,int>{{"top",visible.Top-info.Work.Top},{"bottom",info.Work.Bottom-visible.Bottom},{"left",visible.Left-info.Work.Left},{"right",info.Work.Right-visible.Right}};
  } finally {if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
 }
 public static string AlignHandle(IntPtr handle,int gap,string edgeText) {
  var gaps=new Dictionary<string,int>();
  var bounds=new Dictionary<string,int>();
  var edges=ParseEdges(edgeText,gap,gaps,bounds);
  int gapTop=gaps["top"],gapBottom=gaps["bottom"],gapLeft=gaps["left"],gapRight=gaps["right"];
  if(handle==IntPtr.Zero)throw new Exception("Kein offenes Fenster gefunden. Programm zuerst oeffnen.");
  IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
  if(previous==IntPtr.Zero)throw new Exception("DPI-Modus konnte nicht gesetzt werden.");
  try {
   if(IsIconic(handle)||IsZoomed(handle))throw new Exception("Fenster zuerst aus der Minimierung/Maximierung wiederherstellen.");
   var info=new MONITORINFO();info.Size=Marshal.SizeOf(typeof(MONITORINFO));
   if(!GetMonitorInfo(MonitorFromWindow(handle,2),ref info))throw new Exception("Bildschirmflaeche konnte nicht gelesen werden.");
   RECT outer,visible;
   if(!GetWindowRect(handle,out outer)||DwmGetWindowAttribute(handle,9,out visible,16)!=0)throw new Exception("Sichtbare Fensterkante konnte nicht gemessen werden.");
   // Bezugslinien: Rand des Arbeitsbereichs oder Kante eines Nachbarprogramms.
   int boundTop=bounds.ContainsKey("top")?bounds["top"]:info.Work.Top;
   int boundBottom=bounds.ContainsKey("bottom")?bounds["bottom"]:info.Work.Bottom;
   int boundLeft=bounds.ContainsKey("left")?bounds["left"]:info.Work.Left;
   int boundRight=bounds.ContainsKey("right")?bounds["right"]:info.Work.Right;
   int dx=0,dy=0;
   bool resizeHeight=edges.Contains("top")&&edges.Contains("bottom");
   int width=outer.Right-outer.Left;
   int height=outer.Bottom-outer.Top;
   int heightChange=0;
   if(resizeHeight) {
    int desiredVisibleHeight=boundBottom-boundTop-gapTop-gapBottom;
    if(desiredVisibleHeight<=0)throw new Exception("Arbeitsbereich ist zu klein.");
    heightChange=desiredVisibleHeight-(visible.Bottom-visible.Top);
    height+=heightChange;
    dy=boundTop+gapTop-visible.Top;
   } else {
    if(edges.Contains("bottom"))dy=boundBottom-gapBottom-visible.Bottom;
    if(edges.Contains("top"))dy=boundTop+gapTop-visible.Top;
    // vcenter: senkrecht mittig im Arbeitsbereich (nur ohne Oben/Unten), Größe bleibt erhalten.
    if(edges.Contains("vcenter")&&!edges.Contains("top")&&!edges.Contains("bottom"))dy=info.Work.Top+((info.Work.Bottom-info.Work.Top)-(visible.Bottom-visible.Top))/2-visible.Top;
   }
   // Links + Rechts: volle Breite (wie Oben + Unten volle Höhe).
   bool resizeWidth=edges.Contains("left")&&edges.Contains("right");
   int widthChange=0;
   if(resizeWidth) {
    int desiredVisibleWidth=boundRight-boundLeft-gapLeft-gapRight;
    if(desiredVisibleWidth<=0)throw new Exception("Arbeitsbereich ist zu klein.");
    widthChange=desiredVisibleWidth-(visible.Right-visible.Left);
    width+=widthChange;
    dx=boundLeft+gapLeft-visible.Left;
   } else {
    if(edges.Contains("left"))dx=boundLeft+gapLeft-visible.Left;
    if(edges.Contains("right"))dx=boundRight-gapRight-visible.Right;
    // hcenter: waagerecht mittig im Arbeitsbereich, Größe und Höhe bleiben erhalten.
    if(edges.Contains("hcenter")&&!edges.Contains("left")&&!edges.Contains("right"))dx=info.Work.Left+((info.Work.Right-info.Work.Left)-(visible.Right-visible.Left))/2-visible.Left;
   }
   if(visible.Top+dy<info.Work.Top || visible.Bottom+dy+heightChange>info.Work.Bottom ||
      visible.Left+dx<info.Work.Left || visible.Right+dx+widthChange>info.Work.Right)
    throw new Exception("Fenster passt mit diesem Abstand nicht in den Arbeitsbereich. Bitte zuerst die Breite verkleinern oder vollstaendig auf einen Bildschirm verschieben.");
   uint flags=0x0004|0x0010;
   if(!resizeHeight&&!resizeWidth)flags|=0x0001;
   if(!SetWindowPos(handle,IntPtr.Zero,outer.Left+dx,outer.Top+dy,width,height,flags))
    throw new Exception(MoveError(Marshal.GetLastWin32Error(),"Fenster konnte nicht ausgerichtet werden."));
   Thread.Sleep(400);
   if(DwmGetWindowAttribute(handle,9,out visible,16)!=0)throw new Exception("Neue Fensterkante konnte nicht geprueft werden.");
   var results=new List<string>();
   foreach(string edge in new[]{"top","bottom","left","right","hcenter","vcenter"}.Where(x=>edges.Contains(x))) {
    int actual;string label;
    if(edge=="vcenter") {
     if(edges.Contains("top")||edges.Contains("bottom"))continue;
     int topGap=visible.Top-info.Work.Top,bottomGap=info.Work.Bottom-visible.Bottom;
     if(Math.Abs(topGap-bottomGap)>2)throw new Exception("Mitte senkrecht: nach Verschieben oben "+topGap+" und unten "+bottomGap+" Pixel.");
     results.Add("Mitte ↕: oben "+topGap+" px, unten "+bottomGap+" px");
     continue;
    }
    if(edge=="hcenter") {
     if(edges.Contains("left")||edges.Contains("right"))continue;
     int leftGap=visible.Left-info.Work.Left,rightGap=info.Work.Right-visible.Right;
     if(Math.Abs(leftGap-rightGap)>2)throw new Exception("Mitte: nach Verschieben links "+leftGap+" und rechts "+rightGap+" Pixel.");
     results.Add("Mitte ↔: links "+leftGap+" px, rechts "+rightGap+" px");
     continue;
    }
    switch(edge) {
     case "top":actual=visible.Top-boundTop;label="Oben";break;
     case "bottom":actual=boundBottom-visible.Bottom;label="Unten";break;
     case "left":actual=visible.Left-boundLeft;label="Links";break;
     case "right":actual=boundRight-visible.Right;label="Rechts";break;
     default:continue;
    }
    if(Math.Abs(actual-gaps[edge])>1)throw new Exception(label+": nach Verschieben "+actual+" Pixel statt "+gaps[edge]+".");
    // Programme ohne eigene DPI-Unterstützung lassen sich bei Skalierung (z.B. 175 %) nur in Schritten
    // von mehr als 1 px setzen; dann den Zielwert dazuschreiben, damit die Abweichung erklärt ist.
    results.Add(label+": "+actual+" px"+(actual!=gaps[edge]?" (Ziel "+gaps[edge]+", Windows-Rundung)":""));
   }
   return String.Join(", ",results.ToArray());
  } finally {SetThreadDpiAwarenessContext(previous);}
 }
}
}
