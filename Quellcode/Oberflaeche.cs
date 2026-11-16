// Fensterpositionen wiederherstellen – Windows-11-Oberfläche: Farben, Schriften und eigene Steuerelemente
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
static class Native {
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
 [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd,string app,string idList);
 static int ColorRef(Color c){return c.R|(c.G<<8)|(c.B<<16);}
 public static void DarkTitle(IntPtr handle) {
  try {
   int on=Ui.Dark?1:0;
   if(DwmSetWindowAttribute(handle,20,ref on,4)!=0)DwmSetWindowAttribute(handle,19,ref on,4);
   int caption=ColorRef(Ui.Bg);DwmSetWindowAttribute(handle,35,ref caption,4);
   int text=ColorRef(Ui.TextColor);DwmSetWindowAttribute(handle,36,ref text,4);
  } catch {}
 }
 public static void DarkScroll(Control control) {
  try {SetWindowTheme(control.Handle,Ui.Dark?"DarkMode_Explorer":"Explorer",null);} catch {}
 }
}
public static class Ui {
 public static float S=1f;
 public static int Px(float value){return (int)Math.Round(value*S);}
 // Hintergrund hinter einem Steuerelement: eine Card zeichnet ihre Fläche selbst (Surface), ihr BackColor ist das Fenster.
 public static Color ParentFill(Control c){return c.Parent is Card?Surface:c.Parent!=null?c.Parent.BackColor:Bg;}
 public static bool Dark=true;
 public static Color Bg,Surface,Surface2,SurfaceHover,Border,TextColor,Muted,Accent,LogText,InfoText,Tint;
 public static Color SuccessColor,ErrorColor,WarningColor;
 // Akzentfarben aus Windows (Einstellungen > Personalisierung > Farben), siehe ApplyAccent.
 public static Color Primary,PrimaryHover,PrimaryPressed,OnPrimary,AccentBase;
 // Windows-Einstellung "Standard-App-Modus": AppsUseLightTheme=0 bedeutet dunkel.
 public static bool DetectDark() {
  try {
   using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
    object value=key!=null?key.GetValue("AppsUseLightTheme"):null;
    return value is int&&(int)value==0;
   }
  } catch {return false;}
 }
 public static void ApplyPalette(bool dark) {
  Dark=dark;
  // Windows-11-Farben (neutrales Grau wie Mica/Karten, Systemfarben für Erfolg/Fehler/Warnung).
  if(dark) {
   Bg=Color.FromArgb(32,32,32);Surface=Color.FromArgb(43,43,43);Surface2=Color.FromArgb(50,50,50);
   SurfaceHover=Color.FromArgb(52,52,52);Border=Color.FromArgb(64,64,64);
   TextColor=Color.FromArgb(255,255,255);Muted=Color.FromArgb(160,160,160);Accent=Color.FromArgb(153,235,255);
   LogText=Color.FromArgb(214,214,214);InfoText=Color.FromArgb(222,222,222);Tint=Color.White;
   SuccessColor=Color.FromArgb(108,203,95);ErrorColor=Color.FromArgb(255,153,164);WarningColor=Color.FromArgb(252,225,0);
  } else {
   Bg=Color.FromArgb(243,243,243);Surface=Color.White;Surface2=Color.FromArgb(251,251,251);
   SurfaceHover=Color.FromArgb(246,246,246);Border=Color.FromArgb(229,229,229);
   TextColor=Color.FromArgb(26,26,26);Muted=Color.FromArgb(96,96,96);Accent=Color.FromArgb(0,95,184);
   LogText=Color.FromArgb(51,51,51);InfoText=Color.FromArgb(51,51,51);Tint=Color.Black;
   SuccessColor=Color.FromArgb(15,123,15);ErrorColor=Color.FromArgb(196,43,28);WarningColor=Color.FromArgb(157,93,0);
  }
  ApplyAccent(dark);
 }
 // Windows-Akzentfarbe: AccentPalette enthält die Stufen Hell3, Hell2, Hell1, Basis, Dunkel1, Dunkel2, Dunkel3.
 // Wie Windows 11: hell = Dunkel1 mit weißer Schrift, dunkel = Hell2 mit schwarzer Schrift.
 static Color[] ReadAccentPalette() {
  try {
   using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent")) {
    var data=key!=null?key.GetValue("AccentPalette") as byte[]:null;
    if(data!=null&&data.Length>=28) {
     var colors=new Color[7];
     for(int i=0;i<7;i++)colors[i]=Color.FromArgb(data[i*4],data[i*4+1],data[i*4+2]);
     return colors;
    }
   }
  } catch {}
  Color b=Color.FromArgb(0,120,212);
  try {
   using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM")) {
    object v=key!=null?key.GetValue("AccentColor"):null;
    if(v is int){int abgr=(int)v;b=Color.FromArgb(abgr&0xFF,(abgr>>8)&0xFF,(abgr>>16)&0xFF);}
   }
  } catch {}
  return new[]{Blend(b,Color.White,0.6f),Blend(b,Color.White,0.4f),Blend(b,Color.White,0.2f),b,Blend(b,Color.Black,0.2f),Blend(b,Color.Black,0.4f),Blend(b,Color.Black,0.6f)};
 }
 static double Channel(int v){double c=v/255.0;return c<=0.03928?c/12.92:Math.Pow((c+0.055)/1.055,2.4);}
 static double Luminance(Color c){return 0.2126*Channel(c.R)+0.7152*Channel(c.G)+0.0722*Channel(c.B);}
 static double ContrastRatio(Color a,Color b){double x=Luminance(a)+0.05,y=Luminance(b)+0.05;return x>y?x/y:y/x;}
 // Farbe so lange Richtung "toward" verschieben, bis der Kontrast zu "other" ausreicht (gut lesbar).
 static Color EnsureContrast(Color c,Color other,double min,Color toward) {
  for(int i=0;i<25&&ContrastRatio(c,other)<min;i++)c=Blend(c,toward,0.08f);
  return c;
 }
 static void ApplyAccent(bool dark) {
  Color[] p=ReadAccentPalette();
  AccentBase=p[3];
  // Gleiche Stufen wie im Monitor Dimmer: hell Dunkel1/2/3, dunkel Hell2/1/Basis.
  if(dark) {
   OnPrimary=Color.Black;
   Primary=EnsureContrast(p[1],OnPrimary,4.5,Color.White);
   PrimaryHover=p[2];PrimaryPressed=p[3];
   Accent=EnsureContrast(p[1],Surface,4.5,Color.White);
  } else {
   OnPrimary=Color.White;
   Primary=EnsureContrast(p[4],OnPrimary,4.5,Color.Black);
   PrimaryHover=p[5];PrimaryPressed=p[6];
   Accent=EnsureContrast(p[5],Surface,4.5,Color.Black);
  }
 }
 public static string Hex(Color c){return "#"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2");}
 public static Font UiFont,UiSemibold,TitleFont,HeadingFont,SmallFont,MonoFont,IconFont;
 public static void Init() {
  ApplyPalette(DetectDark());
  using(var g=Graphics.FromHwnd(IntPtr.Zero)) S=g.DpiX/96f;
  if(S<1f)S=1f;
  UiFont=new Font("Segoe UI",10f);
  SmallFont=new Font("Segoe UI",9.5f);
  UiSemibold=Pick(new[]{"Segoe UI Semibold"},10f)??new Font("Segoe UI",10f,FontStyle.Bold);
  TitleFont=Pick(new[]{"Segoe UI Semibold"},17f)??new Font("Segoe UI",17f,FontStyle.Bold);
  HeadingFont=Pick(new[]{"Segoe UI Semibold"},14f)??new Font("Segoe UI",14f,FontStyle.Bold);
  MonoFont=Pick(new[]{"Cascadia Mono","Consolas"},9.5f)??new Font(FontFamily.GenericMonospace,9.5f);
  IconFont=Pick(new[]{"Segoe Fluent Icons","Segoe MDL2 Assets"},11f);
 }
 static Font Pick(string[] names,float size) {
  foreach(string name in names) {
   var font=new Font(name,size);
   if(String.Equals(font.Name,name,StringComparison.OrdinalIgnoreCase))return font;
   font.Dispose();
  }
  return null;
 }
 public static Color Blend(Color a,Color b,float t) {
  return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));
 }
 public static GraphicsPath Round(Rectangle r,int radius) {
  var path=new GraphicsPath();
  int d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
  if(d<=1){path.AddRectangle(r);return path;}
  path.AddArc(r.X,r.Y,d,d,180,90);
  path.AddArc(r.Right-d,r.Y,d,d,270,90);
  path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);
  path.AddArc(r.X,r.Bottom-d,d,d,90,90);
  path.CloseFigure();
  return path;
 }
 // Logo seitenrichtig einpassen; im hellen Modus mit feiner dunkler Kontur, damit das silberne G sichtbar bleibt.
 public static Rectangle FitLogo(Bitmap logo,Rectangle area) {
  float scale=Math.Min((float)area.Width/logo.Width,(float)area.Height/logo.Height);
  int w=(int)(logo.Width*scale),h=(int)(logo.Height*scale);
  return new Rectangle(area.X+(area.Width-w)/2,area.Y+(area.Height-h)/2,w,h);
 }
 public static void DrawLogo(Graphics g,Bitmap logo,Rectangle dest) {
  if(logo==null)return;
  g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
  if(!Dark) {
   var matrix=new System.Drawing.Imaging.ColorMatrix(new float[][]{
    new float[]{0,0,0,0,0},new float[]{0,0,0,0,0},new float[]{0,0,0,0,0},
    new float[]{0,0,0,0.30f,0},new float[]{0,0,0,0,1}});
   using(var attributes=new System.Drawing.Imaging.ImageAttributes()) {
    attributes.SetColorMatrix(matrix);
    int d=Math.Max(1,(int)Math.Round(S*0.8f));
    foreach(var o in new[]{new Point(d,0),new Point(-d,0),new Point(0,d),new Point(0,-d)})
     g.DrawImage(logo,new Rectangle(dest.X+o.X,dest.Y+o.Y,dest.Width,dest.Height),0,0,logo.Width,logo.Height,GraphicsUnit.Pixel,attributes);
   }
  }
  g.DrawImage(logo,dest);
 }
 public static Color StatusColor(string status) {
  if(String.IsNullOrEmpty(status)||status=="Bereit")return Muted;
  if(status.Contains("Fehler")||status.Contains("fehl")||status.StartsWith("Nicht"))return ErrorColor;
  if(status.Contains("…"))return Accent;
  if(status.Contains("übersprungen"))return WarningColor;
  return SuccessColor;
 }
}
public enum ButtonKind {Primary,Secondary,Ghost}
public class ModernButton:Button {
 bool hover,down;
 ButtonKind kind;
 string glyph;
 // Symbolschrift für das Symbol (null = Ui.IconFont), z.B. 10 pt bei kleinen Knöpfen.
 Font glyphFont;
 public Font GlyphFont{get{return glyphFont;}set{glyphFont=value;Invalidate();}}
 // Art und Symbol lassen sich nachträglich ändern (z.B. "Jetzt prüfen" → "Aktualisieren").
 public ButtonKind Kind{get{return kind;}set{kind=value;Font=kind==ButtonKind.Ghost?Ui.UiFont:Ui.UiSemibold;UpdateWidth();Invalidate();}}
 public string Glyph{get{return glyph;}set{glyph=value;UpdateWidth();Invalidate();}}
 // Auswahlfeld im Windows-11-Stil (Globale Richtlinien): Text links, Pfeil (Chevron) rechts, Auswahl über ein Menü.
 bool dropDown;
 public bool DropDown{get{return dropDown;}set{dropDown=value;UpdateWidth();Invalidate();}}
 static Font chevronFont;
 static Font ChevronFont{get{if(chevronFont==null&&Ui.IconFont!=null)chevronFont=new Font(Ui.IconFont.FontFamily,8f);return chevronFont;}}
 public ModernButton(string text,ButtonKind kind,string glyph) {
  this.kind=kind;this.glyph=glyph;
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
  FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;
  Font=kind==ButtonKind.Ghost?Ui.UiFont:Ui.UiSemibold;
  Height=Ui.Px(38);Margin=new Padding(0,0,Ui.Px(8),0);
  Text=text;UpdateWidth();
 }
 bool HasGlyph{get{return Ui.IconFont!=null&&!String.IsNullOrEmpty(glyph);}}
 void UpdateWidth() {
  if(Font==null)return;
  int width=TextRenderer.MeasureText(Text,Font).Width+Ui.Px(28);
  if(HasGlyph)width+=Ui.Px(26);
  if(dropDown)width+=Ui.Px(24);
  Width=width;
 }
 protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);UpdateWidth();Invalidate();}
 protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;down=false;Invalidate();}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){down=true;Invalidate();}}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);down=false;Invalidate();}
 protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);hover=false;down=false;Invalidate();}
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  Color parent=Ui.ParentFill(this);
  g.Clear(parent);
  g.SmoothingMode=SmoothingMode.AntiAlias;
  Color fill,border,fore;
  if(kind==ButtonKind.Primary) {
   fill=!Enabled?Ui.Blend(Ui.Primary,parent,0.6f):down?Ui.PrimaryPressed:hover?Ui.PrimaryHover:Ui.Primary;
   border=fill;
   fore=Enabled?Ui.OnPrimary:Ui.Blend(Ui.OnPrimary,parent,0.5f);
  } else if(kind==ButtonKind.Secondary) {
   fill=!Enabled?Ui.Blend(Ui.Surface2,parent,0.5f):down?Ui.Blend(Ui.Surface2,Ui.Tint,0.12f):hover?Ui.Blend(Ui.Surface2,Ui.Tint,0.05f):Ui.Surface2;
   border=Enabled?Ui.Blend(Ui.Border,Ui.Tint,hover?0.08f:0f):Ui.Blend(Ui.Border,parent,0.5f);
   fore=Enabled?Ui.TextColor:Ui.Blend(Ui.Muted,parent,0.4f);
  } else {
   fill=Enabled&&(hover||down)?Ui.Blend(parent,Ui.Tint,down?0.10f:0.06f):parent;
   border=fill;
   fore=Enabled?Ui.Accent:Ui.Blend(Ui.Accent,parent,0.5f);
  }
  var r=new Rectangle(0,0,Width-1,Height-1);
  using(var path=Ui.Round(r,Ui.Px(4)))
  using(var brush=new SolidBrush(fill))
  using(var pen=new Pen(border)) {g.FillPath(brush,path);g.DrawPath(pen,path);}
  if(Focused&&ShowFocusCues) {
   using(var path=Ui.Round(Rectangle.Inflate(r,-Ui.Px(2),-Ui.Px(2)),Ui.Px(3)))
   using(var pen=new Pen(Ui.Blend(Ui.Accent,Ui.Tint,0.3f),Math.Max(1f,Ui.S))) g.DrawPath(pen,path);
  }
  var flags=TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter;
  int textWidth=TextRenderer.MeasureText(Text,Font,Size.Empty,flags).Width;
  int total=textWidth+(HasGlyph?Ui.Px(26):0);
  int x=(Width-total)/2;
  if(dropDown) {
   // Pfeil rechts, Text links.
   x=Ui.Px(14);
   if(ChevronFont!=null)TextRenderer.DrawText(g,"\uE70D",ChevronFont,new Rectangle(Width-Ui.Px(14)-Ui.Px(14),0,Ui.Px(14),Height),Enabled?Ui.Muted:Ui.Blend(Ui.Muted,parent,0.4f),flags|TextFormatFlags.HorizontalCenter);
  }
  if(HasGlyph) {
   TextRenderer.DrawText(g,glyph,glyphFont??Ui.IconFont,new Rectangle(x,0,Ui.Px(18),Height),fore,flags|TextFormatFlags.HorizontalCenter);
   x+=Ui.Px(26);
  }
  TextRenderer.DrawText(g,Text,Font,new Rectangle(x,0,textWidth+Ui.Px(4),Height),fore,flags|TextFormatFlags.Left);
 }
}
// Ein-/Aus-Schalter im Windows-11-Stil (40 × 20): an = Akzentfarbe mit weißem Knopf rechts.
public class ToggleSwitch:Control {
 bool on,hover;
 public event EventHandler CheckedChanged;
 public bool Checked{get{return on;}set{if(on==value)return;on=value;Invalidate();if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);}}
 public ToggleSwitch() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
  Size=new Size(Ui.Px(40),Ui.Px(20));Cursor=Cursors.Hand;TabStop=true;
 }
 protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
 protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(e.Button==MouseButtons.Left){Focus();Checked=!Checked;}}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space){Checked=!Checked;e.Handled=true;}}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  Color parent=Ui.ParentFill(this);
  g.Clear(parent);
  g.SmoothingMode=SmoothingMode.AntiAlias;
  // Gesperrt: blasser (z.B. "Automatisch aktualisieren", solange "Automatisch prüfen" aus ist).
  Func<Color,Color> fade=c=>Enabled?c:Ui.Blend(c,parent,0.6f);
  bool hot=hover&&Enabled;
  var r=new RectangleF(0.5f,0.5f,Width-1.5f,Height-1.5f);
  using(var path=Ui.Round(Rectangle.Round(r),(int)(r.Height/2))) {
   if(on) {
    using(var brush=new SolidBrush(fade(hot?Ui.PrimaryHover:Ui.Primary)))g.FillPath(brush,path);
   } else {
    using(var brush=new SolidBrush(hot?Ui.Blend(parent,Ui.Tint,0.06f):parent))g.FillPath(brush,path);
    using(var pen=new Pen(fade(Ui.Muted),Math.Max(1f,Ui.S)))g.DrawPath(pen,path);
   }
  }
  float knob=on?Height*0.6f:Height*0.5f;
  float cx=on?Width-Height/2f:Height/2f,cy=Height/2f;
  using(var brush=new SolidBrush(fade(on?Ui.OnPrimary:Ui.Muted)))g.FillEllipse(brush,cx-knob/2,cy-knob/2,knob,knob);
  if(Focused&&ShowFocusCues)using(var pen=new Pen(Ui.Blend(Ui.Accent,Ui.Tint,0.3f))){var f=Rectangle.Inflate(Rectangle.Round(r),-1,-1);using(var p2=Ui.Round(f,f.Height/2))g.DrawPath(pen,p2);}
 }
}
// Einzelnes Symbol der Symbolschrift, nach den tatsächlich gezeichneten Pixeln mittig gesetzt.
public class GlyphView:Control {
 string glyph="";Color color;
 public string Glyph{get{return glyph;}set{glyph=value??"";Invalidate();}}
 public Color GlyphColor{get{return color;}set{color=value;Invalidate();}}
 public GlyphView() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
  color=Ui.TextColor;
 }
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  g.Clear(Ui.ParentFill(this));
  if(Ui.IconFont==null||glyph.Length==0)return;
  int size=Math.Max(Width,Height)*2;
  using(var bmp=new Bitmap(size,size))
  using(var bg=Graphics.FromImage(bmp)) {
   bg.Clear(Color.Transparent);
   bg.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   using(var brush=new SolidBrush(color))bg.DrawString(glyph,Ui.IconFont,brush,size/4f,size/4f);
   int left=size,top=size,right=-1,bottom=-1;
   for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(bmp.GetPixel(x,y).A>24){if(x<left)left=x;if(x>right)right=x;if(y<top)top=y;if(y>bottom)bottom=y;}
   if(right<0)return;
   int w=right-left+1,h=bottom-top+1;
   g.DrawImage(bmp,new Rectangle((Width-w)/2,(Height-h)/2,w,h),new Rectangle(left,top,w,h),GraphicsUnit.Pixel);
  }
 }
}
public class Card:Panel {
 public int Radius;
 public Card() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
  BackColor=Ui.Bg;Radius=Ui.Px(8);
 }
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  g.Clear(Parent!=null?Parent.BackColor:Ui.Bg);
  g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var path=Ui.Round(new Rectangle(0,0,Width-1,Height-1),Radius))
  using(var brush=new SolidBrush(Ui.Surface))
  using(var pen=new Pen(Ui.Border)) {g.FillPath(brush,path);g.DrawPath(pen,path);}
 }
}
public class HeaderPanel:Panel {
 public Bitmap Logo;
 public string Title="",Subtitle="",Badge="";
 public event EventHandler BadgeClick;
 Rectangle badgeRect;
 Font badgeFont;float badgeScale;
 readonly ToolTip tip=new ModernToolTip();
 protected override void OnMouseMove(MouseEventArgs e) {
  base.OnMouseMove(e);
  bool over=badgeRect.Contains(e.Location);
  Cursor=over?Cursors.Hand:Cursors.Default;
  tip.SetToolTip(this,over?"Info anzeigen":"");
 }
 protected override void OnMouseClick(MouseEventArgs e) {
  base.OnMouseClick(e);
  if(e.Button==MouseButtons.Left&&badgeRect.Contains(e.Location)&&BadgeClick!=null)BadgeClick(this,EventArgs.Empty);
 }
 public HeaderPanel() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
  BackColor=Ui.Bg;
 }
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  g.Clear(BackColor);
  int x=0,size=Ui.Px(48);
  if(Logo!=null) {
   g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
   var dest=new Rectangle(0,(Height-size)/2,size,size);
   g.DrawImage(Logo,dest);
   x=dest.Right+Ui.Px(14);
  }
  var flags=TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix;
  Size title=TextRenderer.MeasureText(g,Title,Ui.TitleFont,Size.Empty,flags);
  Size sub=TextRenderer.MeasureText(g,Subtitle,Ui.SmallFont,Size.Empty,flags);
  int gap=Ui.Px(3);
  int y=(Height-title.Height-gap-sub.Height)/2;
  TextRenderer.DrawText(g,Title,Ui.TitleFont,new Point(x,y),Ui.TextColor,flags);
  TextRenderer.DrawText(g,Subtitle,Ui.SmallFont,new Point(x+1,y+title.Height+gap),Ui.Muted,flags);
  if(!String.IsNullOrEmpty(Badge)) {
   // Versions-Plakette (Globale Richtlinien): 12 × Skalierung hinter dem Textende des Namens (gleiche Flags),
   // Höhe 21, Schrift Segoe UI 10,5 px, Innenabstand je 8, senkrecht mittig zum Namen.
   g.SmoothingMode=SmoothingMode.AntiAlias;
   if(badgeFont==null||badgeScale!=Ui.S){if(badgeFont!=null)badgeFont.Dispose();badgeFont=new Font("Segoe UI",10.5f*Ui.S,GraphicsUnit.Pixel);badgeScale=Ui.S;}
   Size b=TextRenderer.MeasureText(g,Badge,badgeFont,Size.Empty,flags);
   int h=Ui.Px(21);
   // Senkrecht mittig zum Namen (Textrahmen mit NoPadding gemessen), ohne zusätzlichen Versatz.
   var pill=new Rectangle(x+title.Width+Ui.Px(12),y+(title.Height-h)/2,b.Width+2*Ui.Px(8),h);
   badgeRect=pill;
   using(var path=Ui.Round(pill,h/2))
   using(var brush=new SolidBrush(Ui.Blend(Ui.Bg,Ui.Accent,0.14f)))
   using(var pen=new Pen(Ui.Blend(Ui.Bg,Ui.Accent,0.38f))) {g.FillPath(brush,path);g.DrawPath(pen,path);}
   TextRenderer.DrawText(g,Badge,badgeFont,pill,Ui.Blend(Ui.Accent,Ui.Tint,0.25f),flags|TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  }
 }
}
public class InfoBar:Control {
 public InfoBar() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
  BackColor=Ui.Bg;
 }
 protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  g.Clear(BackColor);
  g.SmoothingMode=SmoothingMode.AntiAlias;
  var r=new Rectangle(0,0,Width-1,Height-1);
  using(var path=Ui.Round(r,Ui.Px(8)))
  using(var brush=new SolidBrush(Ui.Blend(Ui.Bg,Ui.Accent,0.08f)))
  using(var pen=new Pen(Ui.Blend(Ui.Bg,Ui.Accent,0.28f))) {g.FillPath(brush,path);g.DrawPath(pen,path);}
  int x=Ui.Px(16);
  if(Ui.IconFont!=null) {
   TextRenderer.DrawText(g,"\uE946",Ui.IconFont,new Rectangle(x,0,Ui.Px(20),Height),Ui.Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
   x+=Ui.Px(34);
  }
  var flags=TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.Left;
  var area=new Rectangle(x,0,Math.Max(10,Width-x-Ui.Px(16)),Height);
  Size size=TextRenderer.MeasureText(g,Text,Ui.SmallFont,new Size(area.Width,int.MaxValue),flags);
  area.Y=Math.Max(0,(Height-size.Height)/2);area.Height=size.Height+2;
  TextRenderer.DrawText(g,Text,Ui.SmallFont,area,Ui.InfoText,flags);
 }
}
public class ActivityBar:Control {
 readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
 float position;
 bool active;
 public ActivityBar() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
  BackColor=Ui.Bg;
  timer.Interval=16;
  timer.Tick+=(s,e)=>{position+=0.012f;if(position>1.3f)position=-0.3f;Invalidate();};
 }
 public bool Active {
  get{return active;}
  set{active=value;position=-0.3f;timer.Enabled=value;Invalidate();}
 }
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  g.Clear(BackColor);
  if(!active||Width<=0)return;
  using(var track=new SolidBrush(Ui.Blend(Ui.Bg,Ui.Accent,0.12f))) g.FillRectangle(track,0,0,Width,Height);
  float width=Width*0.3f;
  var segment=new RectangleF(position*Width-width/2,0,width,Height);
  using(var brush=new LinearGradientBrush(new RectangleF(segment.X-1,0,segment.Width+2,Height),Color.Transparent,Color.Transparent,LinearGradientMode.Horizontal)) {
   var blend=new ColorBlend(3);
   blend.Colors=new[]{Color.FromArgb(0,Ui.Accent),Ui.Accent,Color.FromArgb(0,Ui.Accent)};
   blend.Positions=new[]{0f,0.5f,1f};
   brush.InterpolationColors=blend;
   g.FillRectangle(brush,segment);
  }
 }
 protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
public class ActionSeparator:Control {
 public ActionSeparator() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
  BackColor=Ui.Bg;Width=Ui.Px(25);Height=Ui.Px(38);Margin=new Padding(0);
 }
 protected override void OnPaint(PaintEventArgs e) {
  e.Graphics.Clear(BackColor);
  using(var pen=new Pen(Ui.Border)) e.Graphics.DrawLine(pen,Width/2,Ui.Px(6),Width/2,Height-Ui.Px(6));
 }
}
public class ProgramRow {
 internal ProgramList Owner;
 public Entry Entry;
 public string Name;
 bool isChecked;
 string status="Bereit";
 public bool Checked {
  get{return isChecked;}
  set{if(isChecked==value)return;isChecked=value;if(Owner!=null)Owner.RowChanged(true);}
 }
 public string Status {
  get{return status;}
  set{status=value??"";if(Owner!=null)Owner.RowChanged(false);}
 }
}
public class RowMenuEventArgs:EventArgs {
 public ProgramRow Row;
 // Rechtsklick auf eine Gruppenzeile: Name der Gruppe (Row ist dann null).
 public string Group;
 public Point Location;
}
// Programmliste mit Gruppen (ab 2.52): Programme ohne Gruppe stehen oben, darunter je Gruppe eine Überschriftzeile
// (Pfeil zum Ein-/Ausklappen, Kästchen für alle Programme der Gruppe, Name, Anzahl) und ihre Programme.
// Rows enthält nur die Programme, immer in Anzeigereihenfolge; die Gruppe steht in Entry.Group.
public class ProgramList:Panel {
 public readonly List<ProgramRow> Rows=new List<ProgramRow>();
 public event EventHandler CheckedChanged;
 // Rechtsklick auf eine Zeile (Programm oder Gruppe) bzw. auf die freie Fläche.
 public event EventHandler<RowMenuEventArgs> RowMenu;
 // Reihenfolge oder Gruppe per Ziehen (Drag & Drop) oder Strg+Pfeil geändert; Rows ist bereits umsortiert.
 public event EventHandler RowsReordered;
 // Gruppe ein- oder ausgeklappt (Collapsed geändert).
 public event EventHandler GroupsChanged;
 public readonly HashSet<string> Collapsed=new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
 // Eine Zeile der Anzeige: Gruppe (Row==null) oder Programm.
 class Line {public string Group;public ProgramRow Row;}
 readonly List<Line> lines=new List<Line>();
 readonly ToolTip tip=new ModernToolTip();
 int hover=-1,focusIndex;
 // Ziehen: Zeile (Index in lines), Startpunkt, Einfügestelle (0..lines.Count); suppressClick verhindert das Anhaken nach dem Loslassen.
 int dragIndex=-1,dropIndex=-1;
 Point dragStart;
 bool dragging,suppressClick;
 readonly System.Windows.Forms.Timer dragScroll=new System.Windows.Forms.Timer{Interval=40};
 public ProgramList() {
  dragScroll.Tick+=(s,e)=>DragAutoScroll();
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
  SetStyle(ControlStyles.StandardDoubleClick,false);
  BackColor=Ui.Surface;AutoScroll=true;TabStop=true;
 }
 // Ab 2.52 flacher (vorher 42), damit mehr Programme ohne Bildlauf Platz haben.
 public int RowHeight{get{return Ui.Px(32);}}
 public int LineCount{get{return lines.Count;}}
 static string GroupOf(ProgramRow row){return row.Entry!=null&&!String.IsNullOrEmpty(row.Entry.Group)?row.Entry.Group:"";}
 // Gruppennamen in Anzeigereihenfolge (Reihenfolge des ersten Programms je Gruppe).
 public List<string> Groups() {
  var result=new List<string>();
  foreach(var row in Rows){string g=GroupOf(row);if(g.Length>0&&!result.Contains(g,StringComparer.CurrentCultureIgnoreCase))result.Add(g);}
  return result;
 }
 public List<ProgramRow> RowsOf(string group){return Rows.Where(x=>String.Equals(GroupOf(x),group,StringComparison.CurrentCultureIgnoreCase)).ToList();}
 // Zeilenzahl für eine Programmliste (Höhe der Karte vor dem Füllen).
 public static int CountLines(IEnumerable<Entry> entries,IEnumerable<string> collapsed) {
  var closed=new HashSet<string>(collapsed,StringComparer.CurrentCultureIgnoreCase);
  int count=0;
  foreach(var group in entries.GroupBy(x=>x.Group??"",StringComparer.CurrentCultureIgnoreCase))
   count+=group.Key.Length==0?group.Count():1+(closed.Contains(group.Key)?0:group.Count());
  return count;
 }
 // Rows nach Gruppen ordnen (ohne Gruppe zuerst, dann Gruppen in Reihenfolge ihres ersten Programms; innerhalb
 // unverändert) und die Anzeigezeilen neu aufbauen.
 public void Rebuild() {
  var order=Groups();
  var sorted=Rows.Select((row,index)=>new{row,index}).OrderBy(x=>{string g=GroupOf(x.row);return g.Length==0?-1:order.FindIndex(y=>String.Equals(y,g,StringComparison.CurrentCultureIgnoreCase));}).ThenBy(x=>x.index).Select(x=>x.row).ToList();
  Rows.Clear();Rows.AddRange(sorted);
  lines.Clear();
  foreach(var row in Rows)if(GroupOf(row).Length==0)lines.Add(new Line{Row=row});
  foreach(string group in order) {
   lines.Add(new Line{Group=group});
   if(!Collapsed.Contains(group))foreach(var row in RowsOf(group))lines.Add(new Line{Row=row});
  }
  hover=-1;focusIndex=Math.Max(0,Math.Min(focusIndex,lines.Count-1));
  AutoScrollMinSize=new Size(0,lines.Count*RowHeight);
  Invalidate();
 }
 public void Add(ProgramRow row) {
  row.Owner=this;Rows.Add(row);
  Rebuild();
 }
 public void Insert(int index,ProgramRow row) {
  row.Owner=this;Rows.Insert(Math.Max(0,Math.Min(index,Rows.Count)),row);
  Rebuild();
  if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);
 }
 public void Remove(ProgramRow row) {
  if(!Rows.Remove(row))return;
  row.Owner=null;
  Rebuild();
  if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);
 }
 public void SetCollapsed(string group,bool collapsed) {
  if(collapsed?!Collapsed.Add(group):!Collapsed.Remove(group))return;
  Rebuild();
  if(GroupsChanged!=null)GroupsChanged(this,EventArgs.Empty);
 }
 // Alle Programme einer Gruppe an- bzw. abhaken (alle angehakt → alle ab, sonst alle an).
 void ToggleGroup(string group) {
  var members=RowsOf(group);
  bool on=!members.All(x=>x.Checked);
  foreach(var row in members)row.Checked=on;
 }
 string TipText(int index) {
  var line=lines[index];
  if(line.Row==null)return "Gruppe „"+line.Group+"“\r\nKlick: ein-/ausklappen – Kästchen: alle an-/abhaken – Ziehen: Gruppe verschieben – Rechtsklick: umbenennen oder auflösen";
  var row=line.Row;
  string text=row.Name+": "+row.Status;
  if(row.Entry!=null)text+="\r\nZiehen zum Verschieben (auch in eine Gruppe) – Rechtsklick für Gruppe, Sortieren, Ausblenden oder Entfernen";
  return text;
 }
 internal void RowChanged(bool checkedChanged) {
  Invalidate();
  if(hover>=0&&hover<lines.Count)tip.SetToolTip(this,TipText(hover));
  if(checkedChanged&&CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);
 }
 int IndexAt(Point p) {
  int y=p.Y-AutoScrollPosition.Y;
  if(y<0)return -1;
  int index=y/RowHeight;
  return index<lines.Count?index:-1;
 }
 // Kästchen einer Gruppenzeile: bündig mit den Programmen ohne Gruppe; die Programme der Gruppe sind eingerückt.
 Rectangle GroupBox(int index) {
  int cb=Ui.Px(18),top=AutoScrollPosition.Y+index*RowHeight;
  return new Rectangle(Ui.Px(16),top+(RowHeight-cb)/2,cb,cb);
 }
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Native.DarkScroll(this);}
 // Einfügestelle für die Mausposition: zwischen zwei Zeilen, gemessen an der Zeilenmitte.
 int DropAt(Point p) {
  int y=p.Y-AutoScrollPosition.Y;
  return Math.Max(0,Math.Min(lines.Count,(y+RowHeight/2)/RowHeight));
 }
 void DragAutoScroll() {
  if(!dragging){dragScroll.Stop();return;}
  Point p=PointToClient(Cursor.Position);
  int step=0,edge=RowHeight/2;
  if(p.Y<edge)step=-RowHeight/3;else if(p.Y>ClientSize.Height-edge)step=RowHeight/3;
  if(step==0)return;
  AutoScrollPosition=new Point(0,Math.Max(0,-AutoScrollPosition.Y+step));
  dropIndex=DropAt(p);
  Invalidate();
 }
 void EndDrag(bool apply) {
  bool was=dragging;
  int from=dragIndex,to=dropIndex;
  dragging=false;dragIndex=-1;dropIndex=-1;dragScroll.Stop();Capture=false;
  if(!was)return;
  suppressClick=true;
  Invalidate();
  if(!apply||from<0||from>=lines.Count)return;
  if(lines[from].Row==null)DropGroupAfter(lines[from].Group,GroupAbove(to,lines[from].Group));
  else DropRow(lines[from].Row,to);
 }
 // ---- Gruppen verschieben (ab 2.52): Überschrift ziehen oder Strg+Pfeil auf der Überschrift.
 // Gruppe ("" = ohne Gruppe) des Blocks, zu dem eine Zeile gehört.
 string BlockOf(int line){return lines[line].Row==null?lines[line].Group:GroupOf(lines[line].Row);}
 static bool Same(string a,string b){return String.Equals(a??"",b??"",StringComparison.CurrentCultureIgnoreCase);}
 // Gruppe, hinter der eine gezogene Gruppe an der Einfügestelle landet ("" = als erste Gruppe).
 string GroupAbove(int at,string dragged) {
  for(int i=Math.Min(at,lines.Count)-1;i>=0;i--) {
   string block=BlockOf(i);
   if(!Same(block,dragged))return block;
  }
  return "";
 }
 // Einfügelinie beim Ziehen einer Gruppe: am Ende des Blocks der Gruppe darüber bzw. vor der ersten Gruppe.
 int GroupDropLine(string above) {
  if(above.Length==0){int first=lines.FindIndex(x=>x.Row==null);return first<0?lines.Count:first;}
  int last=-1;
  for(int i=0;i<lines.Count;i++)if(Same(BlockOf(i),above))last=i;
  return last+1;
 }
 void DropGroupAfter(string group,string above) {
  var moving=RowsOf(group);
  if(moving.Count==0||Same(group,above))return;
  var rest=Rows.Where(x=>!moving.Contains(x)).ToList();
  int insert=above.Length==0?rest.Count(x=>GroupOf(x).Length==0):rest.FindLastIndex(x=>Same(GroupOf(x),above))+1;
  rest.InsertRange(Math.Max(0,Math.Min(rest.Count,insert)),moving);
  if(rest.SequenceEqual(Rows))return;
  Rows.Clear();Rows.AddRange(rest);
  Rebuild();
  int line=lines.FindIndex(x=>x.Row==null&&Same(x.Group,group));
  if(line>=0)focusIndex=line;
  Invalidate();
  if(RowsReordered!=null)RowsReordered(this,EventArgs.Empty);
 }
 void MoveGroup(string group,int direction) {
  var groups=Groups();
  int index=groups.FindIndex(x=>Same(x,group));
  int target=index+direction;
  if(index<0||target<0||target>=groups.Count)return;
  DropGroupAfter(group,direction<0?(target>0?groups[target-1]:""):groups[target]);
 }
 // Programm an die Einfügestelle (Index in lines) setzen. Die Gruppe ergibt sich aus der Zeile darüber:
 // Gruppenzeile oder Programm einer Gruppe = diese Gruppe; ganz oben bzw. unter einem Programm ohne Gruppe = ohne Gruppe.
 void DropRow(ProgramRow row,int at) {
  at=Math.Max(0,Math.Min(lines.Count,at));
  string group="";
  for(int i=at-1;i>=0;i--) {
   if(lines[i].Row==row)continue;
   group=lines[i].Row==null?lines[i].Group:GroupOf(lines[i].Row);
   break;
  }
  // Unter einer eingeklappten Gruppe: ans Ende dieser Gruppe.
  int rowsBefore=0;
  for(int i=0;i<at;i++)if(lines[i].Row!=null&&lines[i].Row!=row)rowsBefore++;
  if(at>0&&lines[at-1].Row==null&&Collapsed.Contains(lines[at-1].Group)) {
   var members=RowsOf(group).Where(x=>x!=row).ToList();
   rowsBefore=members.Count>0?Rows.Where(x=>x!=row).ToList().IndexOf(members[members.Count-1])+1:rowsBefore;
  }
  string oldGroup=GroupOf(row);
  int oldIndex=Rows.IndexOf(row);
  var rest=Rows.Where(x=>x!=row).ToList();
  rowsBefore=Math.Max(0,Math.Min(rest.Count,rowsBefore));
  if(String.Equals(oldGroup,group,StringComparison.CurrentCultureIgnoreCase)&&rowsBefore==oldIndex)return;
  row.Entry.Group=group.Length==0?null:group;
  rest.Insert(rowsBefore,row);
  Rows.Clear();Rows.AddRange(rest);
  Rebuild();
  int line=lines.FindIndex(x=>x.Row==row);
  if(line>=0)focusIndex=line;
  Invalidate();
  if(RowsReordered!=null)RowsReordered(this,EventArgs.Empty);
 }
 protected override void OnMouseMove(MouseEventArgs e) {
  base.OnMouseMove(e);
  if(dragIndex>=0&&e.Button==MouseButtons.Left&&Enabled) {
   if(!dragging) {
    Size min=SystemInformation.DragSize;
    if(Math.Abs(e.X-dragStart.X)>min.Width||Math.Abs(e.Y-dragStart.Y)>min.Height){dragging=true;Capture=true;dragScroll.Start();tip.Hide(this);}
   }
   if(dragging) {
    int drop=DropAt(e.Location);
    if(drop!=dropIndex){dropIndex=drop;Invalidate();}
    Cursor=Cursors.SizeNS;
    return;
   }
  }
  int index=IndexAt(e.Location);
  if(index!=hover) {
   hover=index;Invalidate();
   tip.SetToolTip(this,index>=0?TipText(index):"");
  }
  Cursor=index>=0?Cursors.Hand:Cursors.Default;
 }
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=-1;Invalidate();}
 protected override void OnMouseDown(MouseEventArgs e) {
  base.OnMouseDown(e);Focus();
  suppressClick=false;
  if(e.Button==MouseButtons.Left&&Enabled&&Rows.Count>1) {
   int index=IndexAt(e.Location);
   // Programme und (bei mehreren Gruppen) Gruppenüberschriften lassen sich ziehen; nicht vom Kästchen aus.
   bool header=index>=0&&lines[index].Row==null;
   bool onBox=header&&Rectangle.Inflate(GroupBox(index),Ui.Px(6),Ui.Px(6)).Contains(e.Location);
   dragIndex=index>=0&&(!header||(!onBox&&Groups().Count>1))?index:-1;
   dragStart=e.Location;
  }
 }
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Left)EndDrag(true);else dragIndex=-1;}
 protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(dragging&&!Capture)EndDrag(false);}
 protected override void OnMouseClick(MouseEventArgs e) {
  base.OnMouseClick(e);
  if(suppressClick){suppressClick=false;return;}
  if(!Enabled)return;
  int index=IndexAt(e.Location);
  // Rechtsklick auf freie Fläche: Menü ohne Zeile (z.B. ausgeblendete Programme wieder anzeigen).
  if(index<0){if(e.Button==MouseButtons.Right&&RowMenu!=null)RowMenu(this,new RowMenuEventArgs{Row=null,Location=e.Location});return;}
  focusIndex=index;
  var line=lines[index];
  if(e.Button==MouseButtons.Right){Invalidate();if(RowMenu!=null)RowMenu(this,new RowMenuEventArgs{Row=line.Row,Group=line.Row==null?line.Group:null,Location=e.Location});return;}
  if(e.Button!=MouseButtons.Left)return;
  if(line.Row!=null){line.Row.Checked=!line.Row.Checked;return;}
  // Gruppenzeile: Kästchen hakt alle an/ab, sonst ein-/ausklappen.
  var box=Rectangle.Inflate(GroupBox(index),Ui.Px(6),Ui.Px(6));
  if(box.Contains(e.Location))ToggleGroup(line.Group);
  else SetCollapsed(line.Group,!Collapsed.Contains(line.Group));
 }
 protected override bool IsInputKey(Keys keyData) {
  if(keyData==Keys.Up||keyData==Keys.Down||keyData==Keys.Left||keyData==Keys.Right||keyData==Keys.Space||keyData==(Keys.Control|Keys.Up)||keyData==(Keys.Control|Keys.Down)||(keyData==Keys.Escape&&dragging))return true;
  return base.IsInputKey(keyData);
 }
 protected override void OnKeyDown(KeyEventArgs e) {
  base.OnKeyDown(e);
  if(lines.Count==0)return;
  if(e.KeyCode==Keys.Escape&&dragging){EndDrag(false);e.Handled=true;return;}
  focusIndex=Math.Max(0,Math.Min(lines.Count-1,focusIndex));
  var line=lines[focusIndex];
  // Strg+Pfeil verschiebt das markierte Programm (wie Ziehen, auch über Gruppengrenzen).
  if(e.Control&&(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down)&&Enabled) {
   if(line.Row!=null)DropRow(line.Row,e.KeyCode==Keys.Up?focusIndex-1:focusIndex+2);
   else MoveGroup(line.Group,e.KeyCode==Keys.Up?-1:1);
  }
  else if(e.KeyCode==Keys.Up)focusIndex=Math.Max(0,focusIndex-1);
  else if(e.KeyCode==Keys.Down)focusIndex=Math.Min(lines.Count-1,focusIndex+1);
  else if(e.KeyCode==Keys.Left&&line.Row==null)SetCollapsed(line.Group,true);
  else if(e.KeyCode==Keys.Right&&line.Row==null)SetCollapsed(line.Group,false);
  else if(e.KeyCode==Keys.Space){if(line.Row!=null)line.Row.Checked=!line.Row.Checked;else ToggleGroup(line.Group);}
  else return;
  focusIndex=Math.Max(0,Math.Min(lines.Count-1,focusIndex));
  int top=focusIndex*RowHeight,visible=-AutoScrollPosition.Y;
  if(top<visible)AutoScrollPosition=new Point(0,top);
  else if(top+RowHeight>visible+ClientSize.Height)AutoScrollPosition=new Point(0,top+RowHeight-ClientSize.Height);
  Invalidate();
  e.Handled=true;
 }
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
 protected override void OnScroll(ScrollEventArgs se){base.OnScroll(se);Invalidate();}
 protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);Invalidate();}
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  g.Clear(BackColor);
  g.SmoothingMode=SmoothingMode.AntiAlias;
  int offset=AutoScrollPosition.Y,h=RowHeight,w=ClientSize.Width;
  var flags=TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis;
  for(int i=0;i<lines.Count;i++) {
   var row=new Rectangle(0,offset+i*h,w,h);
   if(row.Bottom<0||row.Top>ClientSize.Height)continue;
   var line=lines[i];
   bool draggedLine=dragging&&dragIndex>=0&&dragIndex<lines.Count&&(i==dragIndex||lines[dragIndex].Row==null&&line.Row!=null&&Same(GroupOf(line.Row),lines[dragIndex].Group));
   if((i==hover&&Enabled&&!dragging)||draggedLine) {
    using(var path=Ui.Round(Rectangle.Inflate(row,-Ui.Px(2),-Ui.Px(2)),Ui.Px(4)))
    using(var brush=new SolidBrush(Ui.SurfaceHover)) g.FillPath(brush,path);
   }
   if(Focused&&i==focusIndex&&ShowFocusCues) {
    using(var path=Ui.Round(Rectangle.Inflate(row,-Ui.Px(2),-Ui.Px(2)),Ui.Px(4)))
    using(var pen=new Pen(Ui.Blend(Ui.Accent,Ui.Surface,0.3f))) g.DrawPath(pen,path);
   }
   // Trennlinie unter Programmen; über einer Gruppenzeile eine kräftigere Linie über die volle Breite.
   if(i<lines.Count-1&&lines[i+1].Row!=null)
    using(var pen=new Pen(Ui.Blend(Ui.Border,Ui.Surface,0.35f))) g.DrawLine(pen,Ui.Px(14),row.Bottom-1,row.Right-Ui.Px(14),row.Bottom-1);
   if(line.Row==null) {
    if(i>0)using(var pen=new Pen(Ui.Border)) g.DrawLine(pen,Ui.Px(14),row.Top,row.Right-Ui.Px(14),row.Top);
    var members=RowsOf(line.Group);
    bool closed=Collapsed.Contains(line.Group);
    if(Ui.IconFont!=null)
     using(var chevron=new Font(Ui.IconFont.FontFamily,8f))
      TextRenderer.DrawText(g,closed?"\uE76C":"\uE70D",chevron,new Rectangle(row.Right-Ui.Px(40),row.Y,Ui.Px(24),h),Enabled?Ui.Muted:Ui.Border,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
    var gbox=GroupBox(i);
    int on=members.Count(x=>x.Checked);
    DrawCheck(g,gbox,on>0&&on==members.Count,Enabled,on>0&&on<members.Count);
    var nameRect=new Rectangle(gbox.Right+Ui.Px(14),row.Y,Math.Max(0,w-gbox.Right-Ui.Px(70)),h);
    int nameWidth=TextRenderer.MeasureText(g,line.Group,Ui.UiSemibold,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding).Width;
    TextRenderer.DrawText(g,line.Group,Ui.UiSemibold,nameRect,Enabled?Ui.TextColor:Ui.Muted,flags|TextFormatFlags.Left|TextFormatFlags.NoPadding);
    string info=members.Count+(members.Count==1?" Programm":" Programme")+(on>0?", "+on+" ausgewählt":"");
    int infoX=nameRect.X+Math.Min(nameWidth,nameRect.Width)+Ui.Px(12);
    if(infoX<row.Right-Ui.Px(100))
     TextRenderer.DrawText(g,info,Ui.SmallFont,new Rectangle(infoX,row.Y,row.Right-Ui.Px(52)-infoX,h),Ui.Muted,flags|TextFormatFlags.Left|TextFormatFlags.NoPadding);
    continue;
   }
   var item=line.Row;
   // Programme einer Gruppe etwas eingerückt.
   int indent=GroupOf(item).Length>0?Ui.Px(24):0;
   int cb=Ui.Px(18);
   var box=new Rectangle(row.X+Ui.Px(16)+indent,row.Y+(h-cb)/2,cb,cb);
   DrawCheck(g,box,item.Checked,Enabled);
   var nameRect2=new Rectangle(box.Right+Ui.Px(14),row.Y,Ui.Px(170)-indent,h);
   TextRenderer.DrawText(g,item.Name,Ui.UiSemibold,nameRect2,Enabled?Ui.TextColor:Ui.Muted,flags|TextFormatFlags.Left);
   int pillX=nameRect2.Right+Ui.Px(8),maxWidth=row.Right-pillX-Ui.Px(16);
   if(maxWidth<Ui.Px(60))continue;
   Color color=Ui.StatusColor(item.Status);
   bool neutral=color==Ui.Muted;
   int textWidth=TextRenderer.MeasureText(g,item.Status,Ui.SmallFont,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding).Width;
   int pillH=Ui.Px(22);
   var pill=new Rectangle(pillX,row.Y+(h-pillH)/2,Math.Min(maxWidth,textWidth+Ui.Px(40)),pillH);
   using(var path=Ui.Round(pill,pillH/2))
   using(var brush=new SolidBrush(Ui.Blend(BackColor,color,neutral?0.08f:0.14f)))
   using(var pen=new Pen(Ui.Blend(BackColor,color,neutral?0.22f:0.32f))) {g.FillPath(brush,path);g.DrawPath(pen,path);}
   int dot=Ui.Px(7);
   var dotRect=new Rectangle(pill.X+Ui.Px(10),pill.Y+(pillH-dot)/2,dot,dot);
   using(var brush=new SolidBrush(color)) g.FillEllipse(brush,dotRect);
   var textRect=new Rectangle(dotRect.Right+Ui.Px(8),pill.Y,pill.Right-dotRect.Right-Ui.Px(16),pillH);
   TextRenderer.DrawText(g,item.Status,Ui.SmallFont,textRect,neutral?Ui.Muted:Ui.Blend(color,Ui.Tint,Ui.Dark?0.25f:0.1f),flags|TextFormatFlags.Left|TextFormatFlags.NoPadding);
  }
  // Ziehen: Einfügestelle als Linie in Akzentfarbe mit Punkt links (eingerückt, wenn sie in einer Gruppe landet).
  if(dragging&&dropIndex>=0&&dragIndex>=0&&dragIndex<lines.Count) {
   bool groupDrag=lines[dragIndex].Row==null;
   int at=groupDrag?GroupDropLine(GroupAbove(dropIndex,lines[dragIndex].Group)):dropIndex;
   int y=Math.Max(Ui.Px(2),Math.Min(ClientSize.Height-Ui.Px(2),offset+at*h));
   bool inGroup=false;
   if(!groupDrag)for(int i=dropIndex-1;i>=0;i--){if(i==dragIndex)continue;inGroup=lines[i].Row==null||GroupOf(lines[i].Row).Length>0;break;}
   int r=Ui.Px(4),x=Ui.Px(10)+(inGroup?Ui.Px(24):0);
   using(var pen=new Pen(Ui.Accent,Math.Max(2f,2f*Ui.S))) g.DrawLine(pen,x+r,y,w-Ui.Px(10),y);
   using(var brush=new SolidBrush(Ui.Accent)) g.FillEllipse(brush,x-r,y-r,2*r,2*r);
  }
 }
 internal static void DrawCheck(Graphics g,Rectangle box,bool on,bool enabled){DrawCheck(g,box,on,enabled,false);}
 // mixed: einige Programme der Gruppe angehakt (Strich statt Haken).
 internal static void DrawCheck(Graphics g,Rectangle box,bool on,bool enabled,bool mixed) {
  using(var path=Ui.Round(box,Ui.Px(4))) {
   if(on||mixed) {
    using(var brush=new SolidBrush(enabled?Ui.Primary:Ui.Blend(Ui.Primary,Ui.Surface,0.5f))) g.FillPath(brush,path);
    using(var pen=new Pen(Ui.OnPrimary,Math.Max(1.5f,1.8f*Ui.S))) {
     pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
     float x=box.X,y=box.Y,s=box.Width;
     if(mixed)g.DrawLine(pen,x+s*0.28f,y+s*0.5f,x+s*0.72f,y+s*0.5f);
     else g.DrawLines(pen,new[]{new PointF(x+s*0.26f,y+s*0.52f),new PointF(x+s*0.43f,y+s*0.69f),new PointF(x+s*0.75f,y+s*0.33f)});
    }
   } else {
    using(var pen=new Pen(enabled?Ui.Muted:Ui.Border,Math.Max(1f,1.2f*Ui.S))) g.DrawPath(pen,path);
   }
  }
 }
}
// Fußzeile wie im Monitor Dimmer: links Copyright mit Programm und Version,
// rechts GS-Logo mit Name und Website (anklickbar).
public class FooterPanel:Control {
 public const string Url="https://www.westmc002.de";
 const string Name1="Günther Schoder";
 const string Name2="www.westmc002.de";
 public Bitmap Logo;
 public string Caption="";
 Rectangle link;
 bool hover;
 public FooterPanel() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
  BackColor=Ui.Bg;
 }
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  g.Clear(BackColor);
  g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
  int right=Width;
  using(var f1=new Font("Segoe UI Semibold",9.5f))
  using(var f2=new Font("Segoe UI",9f,hover?FontStyle.Underline:FontStyle.Regular))
  using(var f3=new Font("Segoe UI",9f)) {
   var flags=TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix;
   Size s1=TextRenderer.MeasureText(g,Name1,f1,Size.Empty,flags);
   Size s2=TextRenderer.MeasureText(g,Name2,f2,Size.Empty,flags);
   int tw=Math.Max(s1.Width,s2.Width);
   // Globale Richtlinien (Footer, 16.11.2026): Zeilen um 3 × Skalierung überlappen, weil die mit NoPadding
   // gemessene Höhe Leerraum für Ober- und Unterlängen enthält; sonst wirkt der Textblock höher als das Logo.
   int gap=-Ui.Px(3);
   int th=s1.Height+gap+s2.Height;
   int top=(Height-th)/2;
   int tx=right-tw;
   TextRenderer.DrawText(g,Name1,f1,new Point(tx,top),Ui.TextColor,flags);
   TextRenderer.DrawText(g,Name2,f2,new Point(tx,top+s1.Height+gap),hover?Ui.Accent:Ui.Muted,flags);
   link=new Rectangle(tx,top,tw,th);
   if(Logo!=null) {
    int lh=Ui.Px(26);
    int lw=Logo.Width*lh/Math.Max(1,Logo.Height);
    var r=new Rectangle(tx-Ui.Px(10)-lw,(Height-lh)/2,lw,lh);
    g.DrawImage(Logo,r);
    link=Rectangle.Union(link,r);
   }
   Size s3=TextRenderer.MeasureText(g,Caption,f3,Size.Empty,flags);
   TextRenderer.DrawText(g,Caption,f3,new Point(0,(Height-s3.Height)/2),Ui.Muted,flags);
  }
 }
 protected override void OnMouseMove(MouseEventArgs e) {
  bool h=link.Contains(e.Location);
  if(h!=hover){hover=h;Cursor=h?Cursors.Hand:Cursors.Default;Invalidate();}
  base.OnMouseMove(e);
 }
 protected override void OnMouseLeave(EventArgs e) {
  if(hover){hover=false;Cursor=Cursors.Default;Invalidate();}
  base.OnMouseLeave(e);
 }
 protected override void OnMouseClick(MouseEventArgs e) {
  if(e.Button==MouseButtons.Left&&link.Contains(e.Location)) {
   try{Process.Start(new ProcessStartInfo(Url){UseShellExecute=true});}catch{}
  }
  base.OnMouseClick(e);
 }
}
// Kontextmenüs im Windows-11-Stil mit den gleichen Maßen wie Monitor Dimmer und WindowGrid
// (globale Richtlinien, Vorlage WindowGrid ModernMenu.cs): Segoe UI 9,5 pt, Zeilenhöhe = Höhe von "Ag" + 8 × Skalierung,
// Innenabstand je Eintrag 5 × Skalierung, Symbolspalte 36 × Skalierung, Menü-Padding oben 4 × Skalierung,
// Text nach der Buchstabenmitte zentriert, Symbole nach sichtbaren Pixeln zentriert, oben Leerzeile 4 × Skalierung,
// unten MenuSpacer 6 × Skalierung (wie Monitor Dimmer ab v2.37), Abstand Symbolspalte -> Text 8 statt 14 × Skalierung,
// kein alter WinForms-Schatten unter Windows 11.
public static class MenuStyle {
 static Font font;
 public static Font MenuFont{get{if(font==null)font=new Font("Segoe UI",9.5f);return font;}}
 public static Color Back{get{return Ui.Dark?Color.FromArgb(44,44,44):Color.FromArgb(249,249,249);}}
 public static Color Border{get{return Ui.Dark?Color.FromArgb(70,70,70):Color.FromArgb(220,220,220);}}
 public static Color TextColor{get{return Ui.Dark?Color.FromArgb(240,240,240):Color.FromArgb(28,28,28);}}
 public static Color Muted{get{return Ui.Dark?Color.FromArgb(160,160,160):Color.FromArgb(110,110,110);}}
 public static Color Hover{get{return Ui.Dark?Color.FromArgb(62,62,62):Color.FromArgb(234,234,234);}}
 public static Color Separator{get{return Ui.Dark?Color.FromArgb(68,68,68):Color.FromArgb(226,226,226);}}
 static int K(float v){return (int)(v*Ui.S);}
 public static int IconWidth{get{return K(36);}}
 public static int RowHeight{get{return TextRenderer.MeasureText("Ag",MenuFont).Height+K(8);}}
 // Menü vor dem Befüllen einrichten (alte Einträge werden freigegeben).
 public static void Prepare(ContextMenuStrip menu) {
  var old=new ToolStripItem[menu.Items.Count];
  menu.Items.CopyTo(old,0);
  menu.Items.Clear();
  foreach(var item in old) {
   var glyph=item as GlyphMenuItem;
   if(glyph!=null&&glyph.Glyph!=null){glyph.Glyph.Dispose();glyph.Glyph=null;}
   // Gemeinsamer Platzhalter darf nicht mit freigegeben werden.
   var menuItem=item as ToolStripMenuItem;
   if(menuItem!=null)menuItem.Image=null;
   item.Dispose();
  }
  menu.Renderer=new MenuRenderer();
  menu.Font=MenuFont;
  menu.ShowCheckMargin=false;menu.ShowItemToolTips=false;menu.ShowImageMargin=true;
  menu.ImageScalingSize=new Size(IconWidth,K(16));
  // Innenabstand ganz über unsichtbare Leerzeilen (Windows übernimmt Padding bei Menüs nicht zuverlässig).
  menu.Padding=Padding.Empty;
  // Windows 11 zeichnet um runde Menüs selbst einen Schatten; der alte eckige WinForms-Schatten muss aus.
  if(Environment.OSVersion.Version.Build>=22000&&menu.DropShadowEnabled)menu.DropShadowEnabled=false;
 }
 // Abstand von der Symbolspalte bis zum Text in Einheiten (Windows: 14).
 public const int TextGap=8;
 public static GlyphMenuItem Item(string text,char glyph,EventHandler click) {
  var item=new GlyphMenuItem(text);
  int pad=K(5);
  item.Padding=new Padding(0,pad,K(8),pad);
  item.FixedHeight=RowHeight;
  if(glyph!='\0')item.Glyph=Glyph(glyph,TextColor,K(16),K(16));
  // Unsichtbarer Platzhalter: reserviert die volle Breite der Symbolspalte.
  item.Image=Blank(IconWidth,K(16));
  item.ImageScaling=ToolStripItemImageScaling.None;
  if(click!=null)item.Click+=click;
  return item;
 }
 // Eintrag eines Auswahlfelds: der gewählte bekommt den Auswahlpunkt.
 public static GlyphMenuItem Choice(string text,bool selected,EventHandler click) {
  var item=Item(text,'\0',click);
  item.Radio=selected;
  return item;
 }
 // Menü auf den Arbeitsbereich des Bildschirms begrenzen, auf dem das Element liegt (Globale Richtlinien,
 // Kontextmenüs bei mehreren Bildschirmen; Vorlage: Wake on LAN, MenuHelper.ShowOnScreen). Windows verschiebt
 // Menüs sonst nur innerhalb des ganzen Desktops, dann ragen sie auf den Nachbarbildschirm.
 public static void ShowOnScreen(ToolStripDropDown menu,Control owner,Point location,ToolStripDropDownDirection direction) {
  var size=menu.GetPreferredSize(Size.Empty);
  if(!menu.AutoSize||size.Width<=0||size.Height<=0)size=menu.Size;
  var ownerRect=owner.RectangleToScreen(owner.ClientRectangle);
  var area=Screen.FromRectangle(ownerRect).WorkingArea;
  var p=owner.PointToScreen(location);
  bool up=direction==ToolStripDropDownDirection.AboveRight||direction==ToolStripDropDownDirection.AboveLeft;
  bool left=direction==ToolStripDropDownDirection.AboveLeft||direction==ToolStripDropDownDirection.BelowLeft||direction==ToolStripDropDownDirection.Left;
  int x=left?p.X-size.Width:p.X;
  int y=up?p.Y-size.Height:p.Y;
  // Unten kein Platz: nach oben öffnen (bei Auswahlfeldern über dem Element statt darüber hinweg).
  if(!up&&y+size.Height>area.Bottom)y=(location.Y>=owner.Height?ownerRect.Top:p.Y)-size.Height;
  // Oben kein Platz: nach unten öffnen.
  if(up&&y<area.Top)y=p.Y;
  if(x+size.Width>area.Right)x=area.Right-size.Width;
  if(x<area.Left)x=area.Left;
  if(y+size.Height>area.Bottom)y=area.Bottom-size.Height;
  if(y<area.Top)y=area.Top;
  menu.Show(owner,owner.PointToClient(new Point(x,y)),ToolStripDropDownDirection.BelowRight);
 }
 // Unsichtbare Leerzeilen: oben 4 × Skalierung, unter dem letzten Eintrag 6 × Skalierung.
 public static ToolStripItem TopSpacer(){return new MenuSpacer(K(4));}
 public static ToolStripItem BottomSpacer(){return new MenuSpacer(K(6));}
 static Bitmap blank;
 static Image Blank(int width,int height) {
  if(blank==null||blank.Width!=width||blank.Height!=height)blank=new Bitmap(width,height);
  return blank;
 }
 // Symbol auf eine größere Fläche zeichnen und die tatsächlich gezeichneten Pixel mittig setzen.
 public static Bitmap Glyph(char c,Color color,int size,int width) {
  if(Ui.IconFont==null)return null;
  int pad=size;
  var raw=new Bitmap(width+2*pad,size+2*pad);
  using(var g=Graphics.FromImage(raw))
  using(var f=new Font(Ui.IconFont.FontFamily,size*0.78f,GraphicsUnit.Pixel))
  using(var b=new SolidBrush(color))
  using(var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center}) {
   g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   g.DrawString(c.ToString(),f,b,new RectangleF(0,0,raw.Width,raw.Height),sf);
  }
  Rectangle ink=InkBounds(raw);
  var bmp=new Bitmap(width,size);
  if(ink.Width>0&&ink.Height>0) {
   using(var g=Graphics.FromImage(bmp)) {
    float x=(width-ink.Width)/2f,y=(size-ink.Height)/2f;
    g.DrawImage(raw,new RectangleF((float)Math.Round(x),(float)Math.Round(y),ink.Width,ink.Height),new RectangleF(ink.X,ink.Y,ink.Width,ink.Height),GraphicsUnit.Pixel);
   }
  }
  raw.Dispose();
  return bmp;
 }
 static Rectangle InkBounds(Bitmap bmp) {
  int minX=bmp.Width,minY=bmp.Height,maxX=-1,maxY=-1;
  for(int y=0;y<bmp.Height;y++)
   for(int x=0;x<bmp.Width;x++)
    if(bmp.GetPixel(x,y).A>24){if(x<minX)minX=x;if(x>maxX)maxX=x;if(y<minY)minY=y;if(y>maxY)maxY=y;}
  if(maxX<0)return Rectangle.Empty;
  return Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);
 }
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
 // Windows 11: runde Ecken und Schatten für das Menüfenster.
 public static void RoundCorners(ToolStripDropDown menu) {
  try {int pref=2;DwmSetWindowAttribute(menu.Handle,33,ref pref,4);} catch {}
 }
}
public class GlyphMenuItem:ToolStripMenuItem {
 public Image Glyph;
 // Auswahlpunkt (Auswahlfelder, Globale Richtlinien): gefüllter Punkt in Akzentfarbe vor dem gewählten Eintrag.
 public bool Radio;
 // Feste Zeilenhöhe (0 = Windows entscheidet).
 public int FixedHeight;
 public GlyphMenuItem(string text):base(text){}
 public override Size GetPreferredSize(Size constrainingSize) {
  var size=base.GetPreferredSize(constrainingSize);
  if(FixedHeight>0)size.Height=FixedHeight;
  return size;
 }
}
public class MenuSpacer:ToolStripItem {
 readonly int height;
 public MenuSpacer(int height){this.height=height;AutoSize=false;Size=new Size(1,height);}
 public override bool CanSelect{get{return false;}}
 public override Size GetPreferredSize(Size constrainingSize){return new Size(1,height);}
 protected override void OnPaint(PaintEventArgs e){}
}
public class MenuRenderer:ToolStripRenderer {
 readonly Dictionary<ToolStrip,int> marginWidth=new Dictionary<ToolStrip,int>();
 static float K(float v){return v*Ui.S;}
 protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) {
  using(var brush=new SolidBrush(MenuStyle.Back)) e.Graphics.FillRectangle(brush,e.AffectedBounds);
 }
 protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) {
  using(var pen=new Pen(MenuStyle.Border)) e.Graphics.DrawRectangle(pen,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);
 }
 protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){marginWidth[e.ToolStrip]=e.AffectedBounds.Right;}
 protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
  if(!e.Item.Selected||!e.Item.Enabled)return;
  var g=e.Graphics;
  g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var path=Ui.Round(new Rectangle((int)K(4),(int)K(1),e.Item.Width-(int)K(8),e.Item.Height-(int)K(2)),(int)K(4)))
  using(var brush=new SolidBrush(MenuStyle.Hover)) g.FillPath(brush,path);
  // Symbole zeichnet OnRenderItemText – ausgerichtet an der Textmitte, nicht an der Zeile.
 }
 // Senkrechte Mitte der sichtbaren Buchstaben (Windows zentriert sonst die Zeilenhöhe inkl. Unterlängen).
 static float TextVisualCenter(Graphics g,Font f,Rectangle textRect) {
  float center=textRect.Y+textRect.Height/2f;
  try {
   FontFamily ff=f.FontFamily;FontStyle st=f.Style;
   float em=f.SizeInPoints*g.DpiY/72f;
   float up=ff.GetEmHeight(st),asc=ff.GetCellAscent(st),desc=ff.GetCellDescent(st);
   float lineCenter=(asc+desc)/2f/up*em;
   float capCenter=(asc-0.70f*up/2f)/up*em;
   center+=capCenter-lineCenter;
  } catch {}
  return center;
 }
 protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
  Font f=e.TextFont??e.Item.Font;
  Rectangle tr=e.TextRectangle;
  float shift=TextVisualCenter(e.Graphics,f,new Rectangle(0,0,tr.Width,tr.Height))-tr.Height/2f;
  int y=(int)Math.Round((e.Item.Height-tr.Height)/2f-shift);
  e.TextRectangle=new Rectangle(tr.X,y,tr.Width,tr.Height);
  var item=e.Item as GlyphMenuItem;
  if(item!=null&&(item.Glyph!=null||item.Radio)&&e.Text==item.Text) {
   float cy=TextVisualCenter(e.Graphics,f,e.TextRectangle);
   int margin;
   if(item.Owner==null||!marginWidth.TryGetValue(item.Owner,out margin))margin=(int)K(36);
   float cx=margin/2f+K(2);
   if(item.Radio) {
    // Auswahlpunkt wie Wake on LAN: Durchmesser 8 × Skalierung, an der Textmitte ausgerichtet.
    float d=K(8);
    e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
    using(var brush=new SolidBrush(Ui.Accent)) e.Graphics.FillEllipse(brush,cx-d/2,cy-d/2,d,d);
   } else e.Graphics.DrawImage(item.Glyph,(float)Math.Round(cx-item.Glyph.Width/2f),(float)Math.Round(cy-item.Glyph.Height/2f),item.Glyph.Width,item.Glyph.Height);
  }
  e.TextColor=e.Item.Enabled?MenuStyle.TextColor:MenuStyle.Muted;
  e.TextFormat|=TextFormatFlags.EndEllipsis;
  // Abstand Symbol -> Text: Windows setzt den Text 14 Einheiten hinter die Symbolspalte, hier 8 (nur Haupttext).
  var dropDown=e.ToolStrip as ToolStripDropDownMenu;
  if(e.Item.IsOnDropDown&&e.Text==e.Item.Text&&dropDown!=null&&dropDown.ShowImageMargin&&e.TextRectangle.X>K(20)) {
   int gap=(int)K(14-MenuStyle.TextGap);
   var textRect=e.TextRectangle;
   e.TextRectangle=new Rectangle(textRect.X-gap,textRect.Y,textRect.Width+gap,textRect.Height);
  }
  base.OnRenderItemText(e);
 }
 protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) {
  int y=e.Item.Height/2;
  using(var pen=new Pen(MenuStyle.Separator)) e.Graphics.DrawLine(pen,K(12),y,e.Item.Width-K(12),y);
 }
 protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){}
 protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e){}
}
// Fenster mit normalem Windows-Rahmen und fester Größe (globale Richtlinien „Fensterrahmen“):
// Größe in OnLoad festhalten, am Rand keine Größenänderungs-Pfeile (WM_NCHITTEST), dunkle Titelleiste.
public class FixedSizeForm:Form {
 public FixedSizeForm() {
  FormBorderStyle=FormBorderStyle.Sizable;SizeGripStyle=SizeGripStyle.Hide;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
 }
 protected override void OnLoad(EventArgs e) {
  base.OnLoad(e);
  // Als Seite im Hauptfenster eingebettet (TopLevel=false) passt sich die Größe dem Inhaltsbereich an.
  if(TopLevel){MinimumSize=Size;MaximumSize=Size;}
 }
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Native.DarkTitle(Handle);}
 const int WM_NCHITTEST=0x0084,HTCLIENT=1,HTLEFT=10,HTBOTTOMRIGHT=17;
 protected override void WndProc(ref Message m) {
  base.WndProc(ref m);
  if(m.Msg==WM_NCHITTEST) {
   int hit=m.Result.ToInt32();
   if(hit>=HTLEFT&&hit<=HTBOTTOMRIGHT)m.Result=(IntPtr)HTCLIENT;
  }
 }
}
// Eintrag der Navigation links (Globale Richtlinien, Navigation; Vorlage: Wake on LAN, NavButton):
// 40 hoch, Symbol 14 pt (Stecknadel 12 pt), ausgewählt grau hinterlegt mit Akzentbalken links.
public class NavButton:Button {
 bool hover,down,selected;
 char glyph;
 public bool IconOnly;
 public char Glyph{get{return glyph;}set{glyph=value;Invalidate();}}
 public static Color SelectedFill{get{return Ui.Dark?Color.FromArgb(45,45,45):Color.FromArgb(234,234,234);}}
 public static Color HoverFill{get{return Ui.Dark?Color.FromArgb(52,52,52):Color.FromArgb(246,246,246);}}
 public static Color DownFill{get{return Ui.Dark?Color.FromArgb(62,62,62):Color.FromArgb(236,236,236);}}
 public NavButton(string text,char glyph) {
  this.glyph=glyph;
  SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.ResizeRedraw,true);
  FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
  Font=new Font("Segoe UI",9.5f);
  Cursor=Cursors.Hand;
  Height=Ui.Px(40);
  Text=text;
  if(text.Length>0)AccessibleName=text;
 }
 public bool Selected{get{return selected;}set{if(selected!=value){selected=value;Invalidate();}}}
 protected override bool ShowFocusCues{get{return false;}}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=false;down=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){down=true;Invalidate();}base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
 protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  Color back=Parent!=null?Parent.BackColor:Ui.Bg;
  g.Clear(back);
  g.SmoothingMode=SmoothingMode.AntiAlias;
  Color fill=selected&&!IconOnly?SelectedFill:down&&Enabled?DownFill:hover&&Enabled?HoverFill:back;
  Color text=Enabled?Ui.TextColor:Ui.Blend(Ui.Muted,back,0.4f);
  if(fill!=back)
   using(var path=Ui.Round(new Rectangle(0,0,Width-1,Height-1),Ui.Px(5)))
   using(var brush=new SolidBrush(fill)) g.FillPath(brush,path);
  // Symbolgrößen wie Wake on LAN: Einträge und ☰ 14 pt, Stecknadel 12 pt.
  int gs=(int)Math.Round((IconOnly?12f:14f)*96f/72f*Ui.S);
  if(IconOnly){DrawGlyph(g,(Width-gs)/2,gs,text);return;}
  if(selected)
   using(var path=Ui.Round(new Rectangle(0,Height/2-Ui.Px(8),Ui.Px(3),Ui.Px(16)),Math.Max(1,Ui.Px(1))))
   using(var brush=new SolidBrush(Ui.Primary)) g.FillPath(brush,path);
  int x=Ui.Px(14);
  if(glyph!='\0'){DrawGlyph(g,x+(Ui.Px(20)-gs)/2,gs,text);x+=Ui.Px(32);}
  // Eingeklappt: nur das Symbol.
  if(Text.Length>0&&Width-x-Ui.Px(8)>Ui.Px(20))
   using(var bold=selected?new Font("Segoe UI Semibold",9.5f):null)
    TextRenderer.DrawText(g,Text,bold??Font,new Rectangle(x,0,Width-x-Ui.Px(8),Height),text,
     TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
 }
 void DrawGlyph(Graphics g,int x,int size,Color color) {
  using(var image=MenuStyle.Glyph(glyph,color,size,size)) {
   if(image!=null)g.DrawImage(image,x,(Height-size)/2,size,size);
  }
 }
}
public class LogoView:Control {
 public Bitmap Logo;
 public LogoView(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
 protected override void OnPaint(PaintEventArgs e) {
  e.Graphics.Clear(BackColor);
  if(Logo!=null)Ui.DrawLogo(e.Graphics,Logo,Ui.FitLogo(Logo,new Rectangle(Ui.Px(4),Ui.Px(4),Width-Ui.Px(8),Height-Ui.Px(8))));
 }
}
}
