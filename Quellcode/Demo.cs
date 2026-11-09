// Fensterpositionen wiederherstellen – Screenshots mit Beispieldaten (hell und dunkel) für GitHub
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fensterpositionen {
// Erzeugt die Screenshots für die README aus Beispieldaten (Demo-Ansicht).
// Aufruf: "Fensterpositionen wiederherstellen.exe" --screenshots [Zielordner], Standard: Ordner Screenshots neben der EXE.
// Gespeicherte Positionen, Sicherungen und Protokolle werden dabei nicht verändert.
// Ablage: hell.png / dunkel.png (Hauptfenster) und <name>-hell.png / <name>-dunkel.png.
// Jedes Bild wird einmal vor schwarzem und einmal vor weißem Hintergrund aufgenommen; daraus ergeben sich
// durchsichtige runde Ecken und der Windows-Schatten (Vorlage: Monitor Dimmer, Demo.cs).
static class Demo {
 [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attr,out RECT value,int size);
 const int DWMWA_EXTENDED_FRAME_BOUNDS=9;

 public static bool RunScreenshots(string outDir) {
  try {
   MainForm.DemoMode=true;
   TraceLog.Enabled=false;
   Directory.CreateDirectory(outDir);
   try{System.IO.File.Delete(Path.Combine(outDir,"Fehler.txt"));}catch{}
   Icon icon=null;
   try{icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
   // Maus aus dem Bild (rechts unten auf dem Hauptbildschirm).
   Rectangle area=Screen.PrimaryScreen.WorkingArea;
   Cursor.Position=new Point(area.Right-2,area.Bottom-2);
   using(var backdrop=new Backdrop()) {
    foreach(bool dark in new[]{false,true}) {
     Ui.ApplyPalette(dark);
     string suffix=dark?"dunkel":"hell";
     Shot(backdrop,()=>new MainForm(),null,Path.Combine(outDir,suffix+".png"));
     Shot(backdrop,()=>new MainForm(),f=>((MainForm)f).DemoRun(),Path.Combine(outDir,"ablauf-"+suffix+".png"));
     Shot(backdrop,()=>new MainForm(),f=>((MainForm)f).DemoShowEdges(),Path.Combine(outDir,"kanten-"+suffix+".png"));
     Shot(backdrop,()=>new MainForm(),f=>((MainForm)f).DemoShowAdd(),Path.Combine(outDir,"hinzufuegen-"+suffix+".png"));
     Shot(backdrop,()=>new AboutForm(MainForm.LoadSignature(),icon,null,DemoUpdates()),null,Path.Combine(outDir,"info-"+suffix+".png"));
    }
   }
   return true;
  } catch(Exception ex) {
   try{System.IO.File.WriteAllText(Path.Combine(outDir,"Fehler.txt"),ex.ToString());}catch{}
   return false;
  }
 }

 // Screenshots: keine Prüfung, das Info-Fenster zeigt "Aktuell – zuletzt geprüft <heute>".
 static UpdateHooks DemoUpdates() {
  var status=new UpdateStatus{State=UpdateState.Current,Text="Aktuell",Detail="Zuletzt geprüft "+DateTime.Now.ToString("dd.MM.yyyy")};
  return new UpdateHooks{Status=()=>status,Auto=()=>true,SetAuto=on=>{},CheckNow=()=>Task.FromResult(status),CanInstall=()=>false,Install=()=>{},OpenPage=()=>{}};
 }
 static void Shot(Backdrop backdrop,Func<Form> create,Action<Form> prepare,string file) {
  using(var f=create()) {
   f.ShowInTaskbar=false;
   f.StartPosition=FormStartPosition.Manual;
   Rectangle area=Screen.PrimaryScreen.WorkingArea;
   if(prepare!=null){f.CreateControl();prepare(f);}
   f.Location=new Point(area.Left+Math.Max(0,(area.Width-f.Width)/2),area.Top+Math.Max(0,(area.Height-f.Height)/2));
   f.Show();
   f.TopMost=true;
   f.Activate();
   Pump(1500);
   RECT r;
   Rectangle bounds=DwmGetWindowAttribute(f.Handle,DWMWA_EXTENDED_FRAME_BOUNDS,out r,Marshal.SizeOf(typeof(RECT)))==0
    ?Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom):f.Bounds;
   Save(Capture(backdrop,f.Handle,bounds,Margin(f)),file);
   f.Close();
  }
  Pump(300);
 }

 // Rand um das Fenster für den Schatten (24 Einheiten).
 static int Margin(Control c) {
  try{using(var g=c.CreateGraphics())return (int)Math.Round(24*g.DpiX/96f);}
  catch{return 24;}
 }

 // Bereich vor schwarzem und vor weißem Hintergrund aufnehmen und daraus die Durchsichtigkeit berechnen.
 static Bitmap Capture(Backdrop backdrop,IntPtr target,Rectangle bounds,int margin) {
  var r=Rectangle.Inflate(bounds,margin,margin);
  backdrop.Cover(r,Color.Black,target);
  Pump(300);
  using(var onBlack=Grab(r)) {
   backdrop.Cover(r,Color.White,target);
   Pump(300);
   using(var onWhite=Grab(r)) {
    backdrop.Hide();
    return Combine(onBlack,onWhite);
   }
  }
 }

 static Bitmap Grab(Rectangle r) {
  var bmp=new Bitmap(r.Width,r.Height,PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(bmp))g.CopyFromScreen(r.Location,Point.Empty,r.Size);
  return bmp;
 }

 static Bitmap Combine(Bitmap onBlack,Bitmap onWhite) {
  int w=onBlack.Width,h=onBlack.Height;
  var rect=new Rectangle(0,0,w,h);
  var result=new Bitmap(w,h,PixelFormat.Format32bppArgb);
  var bd=onBlack.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
  var wd=onWhite.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
  var od=result.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
  try {
   int n=bd.Stride*h;
   var b=new byte[n];var wh=new byte[n];var o=new byte[n];
   Marshal.Copy(bd.Scan0,b,0,n);
   Marshal.Copy(wd.Scan0,wh,0,n);
   for(int i=0;i+3<n;i+=4) {
    int diff=((wh[i]-b[i])+(wh[i+1]-b[i+1])+(wh[i+2]-b[i+2]))/3;
    int a=Math.Max(0,Math.Min(255,255-diff));
    if(a==0)continue; // ganz durchsichtig
    for(int c=0;c<3;c++)o[i+c]=(byte)Math.Min(255,b[i+c]*255/a);
    o[i+3]=(byte)a;
   }
   Marshal.Copy(o,0,od.Scan0,n);
  } finally {
   onBlack.UnlockBits(bd);onWhite.UnlockBits(wd);result.UnlockBits(od);
  }
  return result;
 }

 static void Save(Bitmap bmp,string file){using(bmp)bmp.Save(file,ImageFormat.Png);}

 // Warten und dabei die Fensternachrichten verarbeiten (Zeichnen, Laden).
 static void Pump(int ms) {
  var sw=Stopwatch.StartNew();
  while(sw.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}
 }

 // Einfarbige Fläche hinter dem aufgenommenen Bereich.
 sealed class Backdrop:Form {
  public Backdrop() {
   FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;TopMost=true;
  }
  protected override bool ShowWithoutActivation{get{return true;}}
  protected override CreateParams CreateParams {
   get{var cp=base.CreateParams;cp.ExStyle|=0x08000000|0x00000080;return cp;} // WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW
  }
  public void Cover(Rectangle r,Color color,IntPtr target) {
   BackColor=color;Bounds=r;
   if(!Visible)Show();
   // vor alle anderen Fenster legen, danach das aufgenommene Fenster wieder davor
   SetWindowPos(Handle,HwndTopmost,0,0,0,0,NoMoveSizeActivate);
   SetWindowPos(target,HwndTopmost,0,0,0,0,NoMoveSizeActivate);
   Refresh();
  }
  static readonly IntPtr HwndTopmost=new IntPtr(-1);
  const uint NoMoveSizeActivate=0x0001|0x0002|0x0010;
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
 }
}
}
