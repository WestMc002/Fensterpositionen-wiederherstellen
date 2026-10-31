// Fensterpositionen wiederherstellen – Programmstart, Programmname und Assembly-Angaben
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Weitere Quelldateien im Ordner Quellcode: Speicher.cs, Ausrichten.cs, Oberflaeche.cs, Dialoge.cs, Hauptfenster.cs.

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

[assembly: AssemblyTitle(Fensterpositionen.AppInfo.Product)]
[assembly: AssemblyProduct(Fensterpositionen.AppInfo.Product)]
[assembly: AssemblyDescription("Fenster sichern, ausrichten und wiederherstellen")]
[assembly: AssemblyCopyright("© 2026 Günther Schoder")]
// Versionsnummer: wird von Erstellen.bat in VersionInfo.cs erzeugt.

namespace Fensterpositionen {
// Version (Hauptversion.Nebenversion.Jahr.MonatTag) steht in VersionInfo.cs, erzeugt von Erstellen.bat.
public static partial class AppInfo {
 public const string Product="Fensterpositionen";
}
static class Program {
 // Fenster unsichtbar (außerhalb des Bildschirms) anzeigen und als PNG speichern.
 // Windows zeichnet die Titelleiste dabei nur grau und inaktiv (auch im dunklen Modus). Deshalb wird nur der
 // Fensterinhalt übernommen und eine Titelleiste im Windows-11-Stil davor gesetzt: Farbe wie das Fenster,
 // Icon, Titel und Fenster-Schaltflächen, feiner Rahmen und runde Ecken (außen durchsichtig).
 static void Capture(Form form,string file) {
  form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.ShowInTaskbar=false;
  form.CreateControl();
  form.Show();
  Application.DoEvents();
  using(var full=new Bitmap(form.Width,form.Height)) {
   form.DrawToBitmap(full,new Rectangle(0,0,form.Width,form.Height));
   Point client=form.PointToScreen(Point.Empty);
   int cx=client.X-form.Left,cy=client.Y-form.Top;
   Size size=form.ClientSize;
   int caption=Ui.Px(32);
   int w=size.Width+2,h=caption+size.Height+2;
   using(var shot=new Bitmap(w,h,System.Drawing.Imaging.PixelFormat.Format32bppArgb))
   using(var g=Graphics.FromImage(shot)) {
    g.Clear(Ui.Bg);
    g.DrawImage(full,new Rectangle(1,1+caption,size.Width,size.Height),new Rectangle(cx,cy,size.Width,size.Height),GraphicsUnit.Pixel);
    DrawCaption(g,form,new Rectangle(1,1,size.Width,caption));
    using(var path=Ui.Round(new Rectangle(0,0,w-1,h-1),Ui.Px(8)))
    using(var outside=new Region(new Rectangle(0,0,w,h))) {
     outside.Exclude(path);
     g.CompositingMode=CompositingMode.SourceCopy;
     using(var clear=new SolidBrush(Color.Transparent)) g.FillRegion(clear,outside);
     g.CompositingMode=CompositingMode.SourceOver;
     g.SmoothingMode=SmoothingMode.AntiAlias;
     using(var pen=new Pen(Ui.Dark?Color.FromArgb(70,70,70):Color.FromArgb(200,200,200))) g.DrawPath(pen,path);
    }
    shot.Save(file,System.Drawing.Imaging.ImageFormat.Png);
   }
  }
  form.Close();
 }
 static void DrawCaption(Graphics g,Form form,Rectangle bar) {
  g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
  int x=bar.X+Ui.Px(12);
  int iconSize=Ui.Px(16);
  using(var icon=MainForm.IconImage(iconSize)) {
   if(icon!=null)g.DrawImage(icon,new Rectangle(x,bar.Y+(bar.Height-iconSize)/2,iconSize,iconSize));
  }
  x+=iconSize+Ui.Px(10);
  var flags=TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding;
  // Schaltflächen rechts: Minimieren, Maximieren (nur Hauptfenster), Schließen.
  var glyphs=new List<string>();
  if(form.MinimizeBox)glyphs.Add("\uE921");
  if(form.MaximizeBox)glyphs.Add("\uE922");
  glyphs.Add("\uE8BB");
  int button=Ui.Px(46),right=bar.Right-glyphs.Count*button;
  using(var font=new Font("Segoe UI",9f))
   TextRenderer.DrawText(g,form.Text,font,new Rectangle(x,bar.Y,Math.Max(0,right-x-Ui.Px(8)),bar.Height),Ui.TextColor,flags|TextFormatFlags.Left);
  if(Ui.IconFont==null)return;
  using(var font=new Font(Ui.IconFont.FontFamily,7f)) {
   for(int i=0;i<glyphs.Count;i++)
    TextRenderer.DrawText(g,glyphs[i],font,new Rectangle(right+i*button,bar.Y,button,bar.Height),Ui.TextColor,flags|TextFormatFlags.HorizontalCenter);
  }
 }
 [STAThread] static void Main(string[] args) {
  if(args.Length>0 && args[0]=="--self-test"){SelfTest.Run();return;}
  try {Native.SetProcessDPIAware();} catch {}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  Ui.Init();
  if(args.Length>0 && args[0]=="--preview"){
   // Vorschaubilder der Oberflaeche in hell und dunkel im Ordner Design\Vorschau erzeugen.
   // Kein Eintrag in die Protokolldatei beim Erzeugen der Vorschau (Erstellen.bat).
   TraceLog.Enabled=false;
   // Demo-Ansicht: keine eigenen Programme und keine Benutzerpfade (Bilder landen auch auf GitHub).
   MainForm.DemoMode=true;
   string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Design","Vorschau");
   Directory.CreateDirectory(dir);
   // Je Farbschema: Hauptfenster, Hauptfenster mit Demo-Ablauf, Info-Fenster, Dialog "Programm hinzufügen".
   Icon icon=null;
   try{icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
   foreach(bool dark in new[]{false,true}) {
    Ui.ApplyPalette(dark);
    string mode=dark?"dunkel":"hell";
    using(var form=new MainForm()) Capture(form,Path.Combine(dir,"Vorschau-"+mode+".png"));
    using(var form=new MainForm()){form.DemoRun();Capture(form,Path.Combine(dir,"Ablauf-"+mode+".png"));}
    using(var about=new AboutForm(MainForm.LoadSignature(),icon,null)) Capture(about,Path.Combine(dir,"Info-"+mode+".png"));
    using(var add=new AddProgramForm(icon,new Entry[0])) Capture(add,Path.Combine(dir,"Hinzufuegen-"+mode+".png"));
   }
   return;
  }
  try {Application.Run(new MainForm());}catch(Exception e){MessageBox.Show(e.Message,"Fensterpositionen",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
}
}
