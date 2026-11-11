// Fensterpositionen wiederherstellen – Meldungen und Rückfragen im Programmstil (statt Windows-MessageBox)
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Fensterpositionen {
// Gleiche Show-Überladungen wie System.Windows.Forms.MessageBox: Innerhalb des Namensraums Fensterpositionen
// nutzen alle vorhandenen MessageBox.Show-Aufrufe damit automatisch das Fenster im Programmdesign (MsgDialog).
public static class MessageBox {
 public static DialogResult Show(string text){return Show(null,text,"",MessageBoxButtons.OK,MessageBoxIcon.None,MessageBoxDefaultButton.Button1);}
 public static DialogResult Show(string text,string caption){return Show(null,text,caption,MessageBoxButtons.OK,MessageBoxIcon.None,MessageBoxDefaultButton.Button1);}
 public static DialogResult Show(string text,string caption,MessageBoxButtons buttons){return Show(null,text,caption,buttons,MessageBoxIcon.None,MessageBoxDefaultButton.Button1);}
 public static DialogResult Show(string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon){return Show(null,text,caption,buttons,icon,MessageBoxDefaultButton.Button1);}
 public static DialogResult Show(string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon,MessageBoxDefaultButton defaultButton){return Show(null,text,caption,buttons,icon,defaultButton);}
 public static DialogResult Show(IWin32Window owner,string text){return Show(owner,text,"",MessageBoxButtons.OK,MessageBoxIcon.None,MessageBoxDefaultButton.Button1);}
 public static DialogResult Show(IWin32Window owner,string text,string caption){return Show(owner,text,caption,MessageBoxButtons.OK,MessageBoxIcon.None,MessageBoxDefaultButton.Button1);}
 public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons){return Show(owner,text,caption,buttons,MessageBoxIcon.None,MessageBoxDefaultButton.Button1);}
 public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon){return Show(owner,text,caption,buttons,icon,MessageBoxDefaultButton.Button1);}
 public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon,MessageBoxDefaultButton defaultButton) {
  // Rückfall nur bei sehr frühen Fehlern, bevor das Design geladen ist.
  if(Ui.UiFont==null)return System.Windows.Forms.MessageBox.Show(owner,text,String.IsNullOrEmpty(caption)?AppInfo.Product:caption,buttons,icon,defaultButton);
  using(var dialog=new MsgDialog(text,caption,buttons,icon,defaultButton,null)) return dialog.ShowFor(owner);
 }
}

// Meldungsfenster: Symbol links als gefüllter Kreis, Text daneben, Knöpfe rechtsbündig unten.
// Optional mit Kästchen (z.B. „Nicht mehr fragen“), Ergebnis in Checked.
public class MsgDialog:FixedSizeForm {
 readonly List<ModernButton> buttons=new List<ModernButton>();
 readonly CheckBox check;
 DialogResult result;
 public bool Checked{get{return check!=null&&check.Checked;}}

 public MsgDialog(string text,string caption,MessageBoxButtons kind,MessageBoxIcon icon,MessageBoxDefaultButton defaultButton,string checkText) {
  Text=AppInfo.Product;
  try {Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);} catch {}
  AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;
  KeyPreview=true;
  int pad=Ui.Px(24),circle=Ui.Px(32),gap=Ui.Px(16);
  int textLeft=pad+(icon==MessageBoxIcon.None?0:circle+gap);
  // Breite nach Textlänge: Fenster mindestens 420, höchstens 600 × Skalierung; lange Zeilen umbrechen.
  int minText=Ui.Px(420)-textLeft-pad,maxText=Ui.Px(600)-textLeft-pad;
  string heading=(caption==null||caption==AppInfo.Product||caption=="Fensterpositionen wiederherstellen")?"":caption.Trim();
  text=text??"";
  var flags=TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding;
  int natural=0;
  foreach(var line in text.Replace("\r\n","\n").Split('\n'))natural=Math.Max(natural,TextRenderer.MeasureText(line,Ui.UiFont,Size.Empty,flags|TextFormatFlags.SingleLine).Width);
  if(heading.Length>0)natural=Math.Max(natural,TextRenderer.MeasureText(heading,Ui.UiSemibold,Size.Empty,flags|TextFormatFlags.SingleLine).Width);
  int textWidth=Math.Max(minText,Math.Min(maxText,natural+Ui.Px(2)));
  int y=pad;
  if(heading.Length>0) {
   int h=TextRenderer.MeasureText(heading,Ui.UiSemibold,new Size(textWidth,0),flags|TextFormatFlags.WordBreak).Height;
   Controls.Add(new Label{Text=heading,Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Bg,UseMnemonic=false,Bounds=new Rectangle(textLeft,y,textWidth,h+Ui.Px(2))});
   y+=h+Ui.Px(8);
  }
  int textHeight=TextRenderer.MeasureText(text,Ui.UiFont,new Size(textWidth,0),flags|TextFormatFlags.WordBreak).Height;
  var body=new Label{Text=text,Font=Ui.UiFont,ForeColor=Ui.TextColor,BackColor=Ui.Bg,UseMnemonic=false,Bounds=new Rectangle(textLeft,y,textWidth,textHeight+Ui.Px(2))};
  Controls.Add(body);
  if(icon!=MessageBoxIcon.None) {
   // Kreis auf Höhe der ersten Zeile (Überschrift bzw. Text), bei einzeiligem Text mittig dazu.
   int firstLine=heading.Length>0?pad:y;
   int lineHeight=TextRenderer.MeasureText("Ag",heading.Length>0?Ui.UiSemibold:Ui.UiFont,Size.Empty,flags).Height;
   int top=heading.Length==0&&textHeight<circle?firstLine+(textHeight-circle)/2:firstLine+Math.Min(0,(lineHeight-circle)/2);
   Controls.Add(new IconCircle(icon){Bounds=new Rectangle(pad,Math.Max(Ui.Px(12),top),circle,circle)});
   y=Math.Max(y+textHeight,Math.Max(Ui.Px(12),top)+circle);
  } else y+=textHeight;
  y+=pad;

  // Knöpfe: Reihenfolge wie bei Windows, deutsche Beschriftungen.
  DialogResult escape;
  switch(kind) {
   case MessageBoxButtons.OKCancel: Add("OK",DialogResult.OK);Add("Abbrechen",DialogResult.Cancel);escape=DialogResult.Cancel;break;
   case MessageBoxButtons.YesNo: Add("Ja",DialogResult.Yes);Add("Nein",DialogResult.No);escape=DialogResult.No;break;
   case MessageBoxButtons.YesNoCancel: Add("Ja",DialogResult.Yes);Add("Nein",DialogResult.No);Add("Abbrechen",DialogResult.Cancel);escape=DialogResult.Cancel;break;
   case MessageBoxButtons.RetryCancel: Add("Wiederholen",DialogResult.Retry);Add("Abbrechen",DialogResult.Cancel);escape=DialogResult.Cancel;break;
   case MessageBoxButtons.AbortRetryIgnore: Add("Abbrechen",DialogResult.Abort);Add("Wiederholen",DialogResult.Retry);Add("Ignorieren",DialogResult.Ignore);escape=DialogResult.Abort;break;
   default: Add("OK",DialogResult.OK);escape=DialogResult.OK;break;
  }
  result=escape;
  int index=defaultButton==MessageBoxDefaultButton.Button3?2:defaultButton==MessageBoxDefaultButton.Button2?1:0;
  if(index>=buttons.Count)index=0;
  var primary=buttons[index];
  primary.Kind=ButtonKind.Primary;
  foreach(var b in buttons)b.Width=Math.Max(Ui.Px(110),b.Width);
  int width=textLeft+textWidth+pad;
  int x=width-pad;
  for(int i=buttons.Count-1;i>=0;i--) {
   x-=buttons[i].Width;
   buttons[i].Location=new Point(x,y);
   x-=Ui.Px(8);
  }
  foreach(var b in buttons)Controls.Add(b);
  if(!String.IsNullOrEmpty(checkText)) {
   check=new CheckBox{Text=checkText,AutoSize=true,Font=Ui.UiFont,ForeColor=Ui.TextColor,BackColor=Ui.Bg,UseMnemonic=false};
   Controls.Add(check);
   check.Location=new Point(pad,y+(Ui.Px(38)-check.PreferredSize.Height)/2);
  }
  AcceptButton=primary;
  ActiveControl=primary;
  // Esc wie bei Windows: Ja/Nein → Nein, OK → OK, sonst Abbrechen.
  KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){result=escape;e.Handled=true;Close();}};
  ClientSize=new Size(width,y+Ui.Px(38)+pad);
 }

 void Add(string text,DialogResult value) {
  var b=new ModernButton(text,ButtonKind.Secondary,null){Margin=new Padding(0)};
  b.Click+=(s,e)=>{result=value;Close();};
  buttons.Add(b);
 }

 // Zeigt das Fenster über dem Besitzer bzw. dem aktiven Programmfenster (nur im selben Thread), sonst bildschirmmittig.
 public DialogResult ShowFor(IWin32Window owner) {
  if(owner==null) {
   var active=Form.ActiveForm;
   if(active!=null&&!active.InvokeRequired&&active.Visible&&active!=this)owner=active;
  }
  var control=owner as Control;
  if(control!=null&&control.InvokeRequired)owner=null;
  StartPosition=owner!=null?FormStartPosition.CenterParent:FormStartPosition.CenterScreen;
  ShowInTaskbar=owner==null;
  if(owner!=null)ShowDialog(owner);else ShowDialog();
  return result;
 }

 // Gefüllter Kreis mit weißem Zeichen, Zeichen nach seinen Umrissen mittig gesetzt.
 class IconCircle:Control {
  readonly Color fill;readonly string sign;
  public IconCircle(MessageBoxIcon icon) {
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
   // MessageBoxIcon: Question=32, Exclamation/Warning=48, Asterisk/Information=64, Hand/Error/Stop=16.
   switch((int)icon) {
    case 32: fill=Ui.Accent;sign="?";break;
    case 48: fill=Ui.WarningColor;sign="!";break;
    case 16: fill=Ui.ErrorColor;sign="×";break;
    default: fill=Ui.Accent;sign="i";break;
   }
  }
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics;
   g.Clear(Ui.Bg);
   g.SmoothingMode=SmoothingMode.AntiAlias;
   using(var brush=new SolidBrush(fill)) g.FillEllipse(brush,0,0,Width-1,Height-1);
   using(var family=new FontFamily("Segoe UI"))
   using(var path=new GraphicsPath()) {
    path.AddString(sign,family,(int)FontStyle.Bold,Height*0.62f,new PointF(0,0),StringFormat.GenericTypographic);
    var ink=path.GetBounds();
    using(var m=new Matrix()) {
     m.Translate((Width-1)/2f-(ink.X+ink.Width/2f),(Height-1)/2f-(ink.Y+ink.Height/2f));
     path.Transform(m);
    }
    using(var white=new SolidBrush(Color.White)) g.FillPath(white,path);
   }
  }
 }
}
// Eingabe eines kurzen Textes (z.B. Name einer Anordnung) im Stil von MsgDialog.
// Knöpfe wie bei Unterdialogen: "Abbrechen" links, Primärknopf ganz rechts; Fehler als rote Zeile, das Fenster bleibt offen.
public class InputDialog:FixedSizeForm {
 readonly TextBox box=new TextBox();
 readonly Label error;
 readonly Func<string,string> validate;
 public string Value{get{return box.Text.Trim();}}
 public InputDialog(string heading,string prompt,string initial,string okText,Func<string,string> validate) {
  this.validate=validate;
  Text=AppInfo.Product;
  try {Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);} catch {}
  AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;
  int pad=Ui.Px(24),width=Ui.Px(460),inner=width-2*pad,y=pad;
  var flags=TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.WordBreak;
  int h=TextRenderer.MeasureText(heading,Ui.UiSemibold,new Size(inner,0),flags).Height;
  Controls.Add(new Label{Text=heading,Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Bg,UseMnemonic=false,Bounds=new Rectangle(pad,y,inner,h+Ui.Px(2))});
  y+=h+Ui.Px(8);
  if(!String.IsNullOrEmpty(prompt)) {
   h=TextRenderer.MeasureText(prompt,Ui.UiFont,new Size(inner,0),flags).Height;
   Controls.Add(new Label{Text=prompt,Font=Ui.UiFont,ForeColor=Ui.TextColor,BackColor=Ui.Bg,UseMnemonic=false,Bounds=new Rectangle(pad,y,inner,h+Ui.Px(2))});
   y+=h+Ui.Px(12);
  }
  var field=new Card{Bounds=new Rectangle(pad,y,inner,Ui.Px(36)),Padding=new Padding(Ui.Px(10),Ui.Px(8),Ui.Px(10),0)};
  box.Dock=DockStyle.Fill;box.BorderStyle=BorderStyle.None;box.BackColor=Ui.Surface;box.ForeColor=Ui.TextColor;box.Font=Ui.UiFont;box.MaxLength=40;
  box.Text=initial??"";
  box.TextChanged+=(s,e)=>error.Text="";
  field.Controls.Add(box);
  Controls.Add(field);
  y+=field.Height+Ui.Px(6);
  error=new Label{Text="",Font=Ui.SmallFont,ForeColor=Ui.ErrorColor,BackColor=Ui.Bg,Tag="error",AutoEllipsis=true,UseMnemonic=false,Bounds=new Rectangle(pad,y,inner,Ui.Px(20))};
  Controls.Add(error);
  y+=Ui.Px(20)+Ui.Px(14);
  var ok=new ModernButton(okText,ButtonKind.Primary,null){Margin=new Padding(0)};
  var cancel=new ModernButton("Abbrechen",ButtonKind.Secondary,null){Margin=new Padding(0)};
  ok.Width=Math.Max(Ui.Px(110),ok.Width);cancel.Width=Math.Max(Ui.Px(110),cancel.Width);
  ok.Location=new Point(width-pad-ok.Width,y);
  cancel.Location=new Point(ok.Left-Ui.Px(10)-cancel.Width,y);
  ok.Click+=(s,e)=>Accept();
  cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};
  Controls.Add(cancel);Controls.Add(ok);
  AcceptButton=ok;CancelButton=cancel;
  ClientSize=new Size(width,y+Ui.Px(38)+pad);
  ActiveControl=box;
  box.SelectAll();
 }
 void Accept() {
  string message=validate!=null?validate(Value):null;
  if(message!=null){error.Text=message;box.Focus();box.SelectAll();return;}
  DialogResult=DialogResult.OK;
  Close();
 }
 // Gibt den eingegebenen Text zurück oder null bei "Abbrechen".
 public static string Ask(IWin32Window owner,string heading,string prompt,string initial,string okText,Func<string,string> validate) {
  using(var dialog=new InputDialog(heading,prompt,initial,okText,validate)) {
   dialog.StartPosition=owner!=null?FormStartPosition.CenterParent:FormStartPosition.CenterScreen;
   var result=owner!=null?dialog.ShowDialog(owner):dialog.ShowDialog();
   return result==DialogResult.OK?dialog.Value:null;
  }
 }
}
// Tooltip im Programmdesign (Globale Richtlinien, Weitere Fenster; Vorlage: Wake on LAN ab v1.61, ModernToolTip):
// Kartenfarbe, 1 px Rand, Segoe UI 9 pt, Innenabstand 8 × 5, Umbruch bei 400, 15 s sichtbar.
public class ModernToolTip:ToolTip {
 static readonly Font TipFont=new Font("Segoe UI",9f);
 // NoPadding: sonst legt Windows beim Messen links und rechts je 3–4 px dazu und der Rand wird größer als vorgegeben.
 const TextFormatFlags Flags=TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.Left|TextFormatFlags.Top;
 const int PadX=8,PadY=5;
 public ModernToolTip() {
  OwnerDraw=true;
  AutoPopDelay=15000;
  Popup+=(s,e)=>{
   // Mit einem Graphics messen, genau wie gezeichnet wird (ohne Gerätekontext rechnet MeasureText einen Zuschlag ein).
   float k;
   Size size;
   string text=GetToolTip(e.AssociatedControl)??"";
   using(var g=Graphics.FromHwnd(IntPtr.Zero)) {
    k=g.DpiX/96f;
    size=TextRenderer.MeasureText(g,text,TipFont,new Size((int)(400*k),0),Flags);
   }
   // 1 px Reserve, damit beim Zeichnen nichts anders umbricht als beim Messen.
   e.ToolTipSize=new Size(size.Width+1+2*(int)(PadX*k),size.Height+2*(int)(PadY*k));
  };
  Draw+=(s,e)=>{
   float k=e.Graphics.DpiX/96f;
   using(var back=new SolidBrush(Ui.Surface)) e.Graphics.FillRectangle(back,e.Bounds);
   using(var pen=new Pen(Ui.Border)) e.Graphics.DrawRectangle(pen,0,0,e.Bounds.Width-1,e.Bounds.Height-1);
   var r=new Rectangle((int)(PadX*k),(int)(PadY*k),e.Bounds.Width-2*(int)(PadX*k),e.Bounds.Height-2*(int)(PadY*k));
   TextRenderer.DrawText(e.Graphics,e.ToolTipText,TipFont,r,Ui.TextColor,Flags);
  };
 }
}
}
