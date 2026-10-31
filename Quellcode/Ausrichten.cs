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
  return IntPtr.Zero;
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
 public static string AlignHandle(IntPtr handle,int gap,string edges) {
  if(handle==IntPtr.Zero)throw new Exception("Kein offenes Fenster gefunden. Programm zuerst oeffnen.");
  IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
  if(previous==IntPtr.Zero)throw new Exception("DPI-Modus konnte nicht gesetzt werden.");
  try {
   if(IsIconic(handle)||IsZoomed(handle))throw new Exception("Fenster zuerst aus der Minimierung/Maximierung wiederherstellen.");
   var info=new MONITORINFO();info.Size=Marshal.SizeOf(typeof(MONITORINFO));
   if(!GetMonitorInfo(MonitorFromWindow(handle,2),ref info))throw new Exception("Bildschirmflaeche konnte nicht gelesen werden.");
   RECT outer,visible;
   if(!GetWindowRect(handle,out outer)||DwmGetWindowAttribute(handle,9,out visible,16)!=0)throw new Exception("Sichtbare Fensterkante konnte nicht gemessen werden.");
   int dx=0,dy=0;
   bool resizeHeight=edges.Contains("top")&&edges.Contains("bottom");
   int width=outer.Right-outer.Left;
   int height=outer.Bottom-outer.Top;
   int heightChange=0;
   if(resizeHeight) {
    int desiredVisibleHeight=info.Work.Bottom-info.Work.Top-2*gap;
    if(desiredVisibleHeight<=0)throw new Exception("Arbeitsbereich ist zu klein.");
    heightChange=desiredVisibleHeight-(visible.Bottom-visible.Top);
    height+=heightChange;
    dy=info.Work.Top+gap-visible.Top;
   } else {
    if(edges.Contains("bottom"))dy=info.Work.Bottom-gap-visible.Bottom;
    if(edges.Contains("top"))dy=info.Work.Top+gap-visible.Top;
   }
   if(edges.Contains("left"))dx=info.Work.Left+gap-visible.Left;
   if(edges.Contains("right"))dx=info.Work.Right-gap-visible.Right;
   // hcenter: waagerecht mittig im Arbeitsbereich, Größe und Höhe bleiben erhalten.
   if(edges.Contains("hcenter"))dx=info.Work.Left+((info.Work.Right-info.Work.Left)-(visible.Right-visible.Left))/2-visible.Left;
   if(visible.Top+dy<info.Work.Top || visible.Bottom+dy+heightChange>info.Work.Bottom ||
      visible.Left+dx<info.Work.Left || visible.Right+dx>info.Work.Right)
    throw new Exception("Fenster passt mit diesem Abstand nicht in den Arbeitsbereich. Bitte zuerst die Breite verkleinern oder vollstaendig auf einen Bildschirm verschieben.");
   uint flags=0x0004|0x0010;
   if(!resizeHeight)flags|=0x0001;
   if(!SetWindowPos(handle,IntPtr.Zero,outer.Left+dx,outer.Top+dy,width,height,flags))
    throw new Exception(MoveError(Marshal.GetLastWin32Error(),"Fenster konnte nicht ausgerichtet werden."));
   Thread.Sleep(400);
   if(DwmGetWindowAttribute(handle,9,out visible,16)!=0)throw new Exception("Neue Fensterkante konnte nicht geprueft werden.");
   var results=new List<string>();
   foreach(string edge in edges.Split(' ')) {
    int actual;string label;
    if(edge=="hcenter") {
     int leftGap=visible.Left-info.Work.Left,rightGap=info.Work.Right-visible.Right;
     if(Math.Abs(leftGap-rightGap)>2)throw new Exception("Mitte: nach Verschieben links "+leftGap+" und rechts "+rightGap+" Pixel.");
     results.Add("Mittig: links "+leftGap+" px, rechts "+rightGap+" px");
     continue;
    }
    switch(edge) {
     case "top":actual=visible.Top-info.Work.Top;label="Oben";break;
     case "bottom":actual=info.Work.Bottom-visible.Bottom;label="Unten";break;
     case "left":actual=visible.Left-info.Work.Left;label="Links";break;
     case "right":actual=info.Work.Right-visible.Right;label="Rechts";break;
     default:continue;
    }
    if(Math.Abs(actual-gap)>1)throw new Exception(label+": nach Verschieben "+actual+" Pixel statt "+gap+".");
    results.Add(label+": "+actual+" px");
   }
   return String.Join(", ",results.ToArray());
  } finally {SetThreadDpiAwarenessContext(previous);}
 }
}
}
