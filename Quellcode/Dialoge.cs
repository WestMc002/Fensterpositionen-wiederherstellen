// Fensterpositionen wiederherstellen – Info-Fenster und Dialog „Programm hinzufügen“
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
public class AboutForm:FixedSizeForm {
 public AboutForm(Bitmap logo,Icon icon,EventHandler openBackups) {
  Text="Info über "+AppInfo.Product;
  // Normaler Rahmen wie die Hauptfenster (Icon mit gleichem Abstand), Größe fest: siehe FixedSizeForm.
  StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;Icon=icon;
  int w=Ui.Px(540),pad=Ui.Px(32),y=Ui.Px(16);
  if(logo!=null) {
   Controls.Add(new LogoView{Logo=logo,BackColor=Ui.Bg,Bounds=new Rectangle(pad,y,w-2*pad,Ui.Px(150))});
   y+=Ui.Px(158);
  }
  Controls.Add(new Label{Text="Fensterpositionen wiederherstellen",Font=Ui.HeadingFont,ForeColor=Ui.TextColor,BackColor=Ui.Bg,TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(0,y,w,Ui.Px(34))});
  y+=Ui.Px(34);
  Controls.Add(new Label{Text="Version "+AppInfo.Version,Font=Ui.SmallFont,ForeColor=Ui.Accent,BackColor=Ui.Bg,TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(0,y,w,Ui.Px(24))});
  y+=Ui.Px(36);
  string built="";int year=DateTime.Now.Year;
  try{DateTime stamp=System.IO.File.GetLastWriteTime(Application.ExecutablePath);built=stamp.ToString("dd.MM.yyyy");year=stamp.Year;}catch{}
  string root=Store.Root;
  string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  if(root.StartsWith(local,StringComparison.OrdinalIgnoreCase))root="%LOCALAPPDATA%"+root.Substring(local.Length);
  var lines=new[]{new[]{"Entwickler","Günther Schoder"},new[]{"Erstellt",built},new[]{"Farbschema",(Ui.Dark?"dunkel":"hell")+" (folgt Windows)"},new[]{"Positionen",root}};
  int lineH=Ui.Px(26),cardH=lines.Length*lineH+Ui.Px(24);
  var card=new Card{Bounds=new Rectangle(pad,y,w-2*pad,cardH),Padding=new Padding(0)};
  for(int i=0;i<lines.Length;i++) {
   card.Controls.Add(new Label{Text=lines[i][0],Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(16),Ui.Px(12)+i*lineH,Ui.Px(96),lineH)});
   card.Controls.Add(new Label{Text=lines[i][1],Font=Ui.SmallFont,ForeColor=Ui.TextColor,BackColor=Ui.Surface,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(112),Ui.Px(12)+i*lineH,card.Width-Ui.Px(124),lineH)});
  }
  Controls.Add(card);
  y+=cardH+Ui.Px(18);
  string guide=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Anleitung.txt");
  var guideButton=new ModernButton("Anleitung",ButtonKind.Secondary,"\uE8A5");
  guideButton.Enabled=System.IO.File.Exists(guide);
  guideButton.Click+=(s,e)=>{try{Process.Start(guide);}catch(Exception ex){MessageBox.Show(this,ex.Message);}};
  var backupButton=new ModernButton("Sicherungen",ButtonKind.Secondary,"\uE838");
  backupButton.Click+=openBackups;
  var traceButton=new ModernButton("Protokolle",ButtonKind.Secondary,"\uE81C");
  traceButton.Click+=(s,e)=>{
   try{Directory.CreateDirectory(TraceLog.Folder);Process.Start("explorer.exe","\""+TraceLog.Folder+"\"");}
   catch(Exception ex){MessageBox.Show(this,ex.Message);}
  };
  var closeButton=new ModernButton("Schließen",ButtonKind.Primary,null);
  closeButton.DialogResult=DialogResult.OK;
  var buttons=new[]{guideButton,backupButton,traceButton,closeButton};
  int total=0;foreach(var b in buttons)total+=b.Width;total+=Ui.Px(8)*(buttons.Length-1);
  int bx=(w-total)/2;
  foreach(var b in buttons){b.Location=new Point(bx,y);bx+=b.Width+Ui.Px(8);Controls.Add(b);}
  AcceptButton=closeButton;CancelButton=closeButton;
  y+=Ui.Px(38)+Ui.Px(16);
  Controls.Add(new Label{Text="© "+year+" Günther Schoder",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(0,y,w,Ui.Px(22))});
  y+=Ui.Px(22)+Ui.Px(18);
  ClientSize=new Size(w,y);
 }
}

// Dialog "Programm hinzufügen": listet geöffnete Fenster; gespeichert wird nur die Fensterposition.
public class AddProgramForm:FixedSizeForm {
 class WindowChoice {
  public string Title,Process,Exe,Description;
 }
 readonly ListBox windows=new ListBox();
 readonly TextBox nameBox=new TextBox();
 readonly ModernButton addButton;
 readonly HashSet<string> excluded;
 readonly HashSet<string> usedIds;
 public Entry Result;
 public AddProgramForm(Icon icon,IEnumerable<Entry> existing) {
  excluded=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  usedIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var entry in existing) {
   if(!String.IsNullOrEmpty(entry.ProcessName))excluded.Add(entry.ProcessName);
   if(!String.IsNullOrEmpty(entry.AltProcessName))excluded.Add(entry.AltProcessName);
   usedIds.Add(entry.Id);
  }
  Text="Programm hinzufügen";
  StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;Icon=icon;
  int w=Ui.Px(560),pad=Ui.Px(24),y=Ui.Px(18),inner=w-2*pad;
  Controls.Add(new Label{Text="Programm hinzufügen",Font=Ui.HeadingFont,ForeColor=Ui.TextColor,BackColor=Ui.Bg,Bounds=new Rectangle(pad,y,inner,Ui.Px(32))});
  y+=Ui.Px(34);
  Controls.Add(new Label{Text="Wähle ein geöffnetes Fenster. Gespeichert wird nur seine Position und Größe – das Programm wird dafür nicht beendet.",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",Bounds=new Rectangle(pad,y,inner,Ui.Px(40))});
  y+=Ui.Px(46);
  var listCard=new Card{Bounds=new Rectangle(pad,y,inner,Ui.Px(268)),Padding=new Padding(Ui.Px(4))};
  windows.Dock=DockStyle.Fill;windows.BorderStyle=BorderStyle.None;windows.IntegralHeight=false;
  windows.DrawMode=DrawMode.OwnerDrawFixed;windows.ItemHeight=Ui.Px(44);
  windows.BackColor=Ui.Surface;windows.ForeColor=Ui.TextColor;
  windows.DrawItem+=DrawWindow;
  windows.HandleCreated+=(s,e)=>Native.DarkScroll(windows);
  windows.SelectedIndexChanged+=(s,e)=>{
   var choice=windows.SelectedItem as WindowChoice;
   if(choice!=null)nameBox.Text=String.IsNullOrEmpty(choice.Description)?choice.Process:choice.Description;
   UpdateButtons();
  };
  windows.DoubleClick+=(s,e)=>{if(windows.SelectedItem!=null)Accept();};
  listCard.Controls.Add(windows);
  Controls.Add(listCard);
  y+=listCard.Height+Ui.Px(14);
  Controls.Add(new Label{Text="Name in der Liste",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",Bounds=new Rectangle(pad,y,inner,Ui.Px(22))});
  y+=Ui.Px(24);
  var nameCard=new Card{Bounds=new Rectangle(pad,y,inner,Ui.Px(36)),Padding=new Padding(Ui.Px(10),Ui.Px(8),Ui.Px(10),0)};
  nameBox.Dock=DockStyle.Fill;nameBox.BorderStyle=BorderStyle.None;nameBox.BackColor=Ui.Surface;nameBox.ForeColor=Ui.TextColor;nameBox.Font=Ui.UiFont;
  nameBox.TextChanged+=(s,e)=>UpdateButtons();
  nameCard.Controls.Add(nameBox);
  Controls.Add(nameCard);
  y+=nameCard.Height+Ui.Px(20);
  var refresh=new ModernButton("Aktualisieren",ButtonKind.Secondary,"");
  refresh.Location=new Point(pad,y);
  refresh.Click+=(s,e)=>LoadWindows();
  Controls.Add(refresh);
  addButton=new ModernButton("Hinzufügen",ButtonKind.Primary,null);
  var cancel=new ModernButton("Abbrechen",ButtonKind.Secondary,null);
  cancel.DialogResult=DialogResult.Cancel;
  cancel.Location=new Point(w-pad-cancel.Width,y);
  addButton.Location=new Point(cancel.Left-Ui.Px(8)-addButton.Width,y);
  addButton.Click+=(s,e)=>Accept();
  Controls.Add(addButton);Controls.Add(cancel);
  CancelButton=cancel;
  y+=Ui.Px(38)+Ui.Px(20);
  ClientSize=new Size(w,y);
  LoadWindows();
 }
 void UpdateButtons(){addButton.Enabled=windows.SelectedItem!=null&&nameBox.Text.Trim().Length>0;}
 void LoadWindows() {
  var choices=new List<WindowChoice>();
  if(MainForm.DemoMode) {
   // Screenshots: feste Demo-Liste statt der tatsächlich offenen Fenster (deren Titel privat sein können).
   choices.Add(new WindowChoice{Title="Downloads – Datei-Explorer",Process="explorer",Exe=@"C:\Windows\explorer.exe",Description="Windows-Explorer"});
   choices.Add(new WindowChoice{Title="Paint",Process="mspaint",Exe=@"C:\Windows\System32\mspaint.exe",Description="Paint"});
   choices.Add(new WindowChoice{Title="Rechner",Process="CalculatorApp",Exe=@"C:\Program Files\WindowsApps\CalculatorApp.exe",Description="Rechner"});
   choices.Add(new WindowChoice{Title="Terminal",Process="WindowsTerminal",Exe=@"C:\Program Files\WindowsApps\WindowsTerminal.exe",Description="Terminal"});
   choices.Add(new WindowChoice{Title="Unbenannt – Editor",Process="Notepad",Exe=@"C:\Windows\System32\notepad.exe",Description="Editor"});
   windows.BeginUpdate();windows.Items.Clear();
   foreach(var c in choices)windows.Items.Add(c);
   windows.EndUpdate();
   windows.SelectedIndex=choices.Count-1;
   UpdateButtons();
   return;
  }
  int own=Process.GetCurrentProcess().Id;
  foreach(var process in Process.GetProcesses()) using(process) {
   try {
    if(process.Id==own||process.MainWindowHandle==IntPtr.Zero)continue;
    string title=process.MainWindowTitle;
    if(String.IsNullOrEmpty(title)||excluded.Contains(process.ProcessName))continue;
    var choice=new WindowChoice{Title=title,Process=process.ProcessName};
    // Pfad und Beschreibung sind bei Programmen mit Administratorrechten ohne eigene Adminrechte nicht lesbar.
    try {
     choice.Exe=process.MainModule.FileName;
     choice.Description=FileVersionInfo.GetVersionInfo(choice.Exe).FileDescription;
     if(choice.Description!=null) {
      choice.Description=choice.Description.Trim();
      // Manche Programme (z.B. der Windows-11-Editor) tragen nur den Dateinamen ein: "Notepad.exe" -> "Notepad".
      if(choice.Description.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))choice.Description=choice.Description.Substring(0,choice.Description.Length-4);
     }
    } catch {}
    choices.Add(choice);
   } catch {}
  }
  choices.Sort((a,b)=>String.Compare(a.Title,b.Title,StringComparison.CurrentCultureIgnoreCase));
  windows.BeginUpdate();
  windows.Items.Clear();
  foreach(var c in choices)windows.Items.Add(c);
  windows.EndUpdate();
  if(windows.Items.Count>0)windows.SelectedIndex=0;
  else nameBox.Text="";
  UpdateButtons();
 }
 void DrawWindow(object sender,DrawItemEventArgs e) {
  var g=e.Graphics;
  using(var back=new SolidBrush(Ui.Surface)) g.FillRectangle(back,e.Bounds);
  if(e.Index<0||e.Index>=windows.Items.Count)return;
  var choice=(WindowChoice)windows.Items[e.Index];
  bool selected=(e.State&DrawItemState.Selected)==DrawItemState.Selected;
  g.SmoothingMode=SmoothingMode.AntiAlias;
  var r=Rectangle.Inflate(e.Bounds,-Ui.Px(2),-Ui.Px(2));
  if(selected) {
   using(var path=Ui.Round(r,Ui.Px(4)))
   using(var brush=new SolidBrush(Ui.Blend(Ui.Surface,Ui.Accent,0.16f))) g.FillPath(brush,path);
   // Schmale Akzentmarke links wie in Windows-11-Listen.
   using(var path=Ui.Round(new Rectangle(r.X+Ui.Px(2),r.Y+r.Height/2-Ui.Px(8),Ui.Px(3),Ui.Px(16)),Ui.Px(1)))
   using(var brush=new SolidBrush(Ui.Accent)) g.FillPath(brush,path);
  }
  var flags=TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis|TextFormatFlags.Left|TextFormatFlags.NoPadding;
  int x=r.X+Ui.Px(14),width=r.Width-Ui.Px(24);
  TextRenderer.DrawText(g,choice.Title,Ui.UiSemibold,new Rectangle(x,r.Y+Ui.Px(4),width,Ui.Px(20)),Ui.TextColor,flags);
  string detail=choice.Process+".exe"+(String.IsNullOrEmpty(choice.Description)?"":" · "+choice.Description)+(choice.Exe==null?" · Pfad nicht lesbar (Administrator?)":"");
  TextRenderer.DrawText(g,detail,Ui.SmallFont,new Rectangle(x,r.Y+Ui.Px(22),width,Ui.Px(18)),Ui.Muted,flags);
 }
 void Accept() {
  var choice=windows.SelectedItem as WindowChoice;
  string name=nameBox.Text.Trim();
  if(choice==null||name.Length==0)return;
  var id=new StringBuilder("eigen-");
  foreach(char c in choice.Process.ToLowerInvariant())id.Append(Char.IsLetterOrDigit(c)?c:'-');
  string baseId=id.ToString(),unique=baseId;
  for(int i=2;usedIds.Contains(unique);i++)unique=baseId+"-"+i;
  Result=new Entry{Id=unique,Name=name,ProcessName=choice.Process,Exe=choice.Exe,WindowOnly=true,Custom=true,Keys=new[]{"Window"}};
  DialogResult=DialogResult.OK;
  Close();
 }
}

// Dialog "Kanten je Programm": je Programm die Kanten festlegen, an denen ausgerichtet wird.
public class AlignRulesForm:FixedSizeForm {
 static readonly string[] Directions={"top","bottom","left","right","hcenter","vcenter"};
 static readonly string[] Titles={"Oben","Unten","Links","Rechts","↔ Mitte","↕ Mitte"};
 readonly List<Entry> programs;
 readonly Dictionary<string,CheckCell[]> cells=new Dictionary<string,CheckCell[]>(StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,HashSet<string>> Rules;
 public AlignRulesForm(Icon icon,IEnumerable<Entry> list,Dictionary<string,HashSet<string>> current) {
  programs=new List<Entry>(list);
  Rules=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
  Text="Kanten je Programm";
  StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;Icon=icon;
  int w=Ui.Px(640),pad=Ui.Px(24),y=Ui.Px(18),inner=w-2*pad;
  Controls.Add(new Label{Text="Kanten je Programm",Font=Ui.HeadingFont,ForeColor=Ui.TextColor,BackColor=Ui.Bg,Bounds=new Rectangle(pad,y,inner,Ui.Px(32))});
  y+=Ui.Px(34);
  Controls.Add(new Label{Text="„Automatisch“ richtet alle angehakten Kanten zusammen aus (Oben + Unten = volle Höhe, Links + Rechts = volle Breite, ↔ Mitte = waagerecht, ↕ Mitte = senkrecht mittig). Die einzelnen Schaltflächen richten nur Programme aus, bei denen die Spalte angehakt ist.",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",Bounds=new Rectangle(pad,y,inner,Ui.Px(60))});
  y+=Ui.Px(66);
  int head=Ui.Px(36),rowH=Ui.Px(40),colW=Ui.Px(66);
  int visibleRows=Math.Max(1,Math.Min(programs.Count,9));
  var card=new Card{Bounds=new Rectangle(pad,y,inner,head+visibleRows*rowH+Ui.Px(10)),Padding=new Padding(Ui.Px(1))};
  int colX=inner-Ui.Px(12)-Directions.Length*colW;
  var header=new Panel{Dock=DockStyle.Top,Height=head,BackColor=Ui.Surface};
  header.Controls.Add(new Label{Text="Programm",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,Tag="muted",TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(16),0,colX-Ui.Px(16),head)});
  for(int i=0;i<Titles.Length;i++)
   header.Controls.Add(new Label{Text=Titles[i],Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,Tag="muted",TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(colX+i*colW,0,colW,head)});
  var rows=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Ui.Surface};
  rows.HandleCreated+=(s,e)=>Native.DarkScroll(rows);
  for(int r=0;r<programs.Count;r++) {
   var entry=programs[r];
   int top=r*rowH;
   rows.Controls.Add(new Label{Text=entry.Name+(entry.Custom?"  (Eigenes)":""),Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Surface,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(16),top,colX-Ui.Px(20),rowH)});
   var row=new CheckCell[Directions.Length];
   HashSet<string> allowed;
   if(!current.TryGetValue(entry.Id,out allowed))allowed=MainForm.DefaultEdgesFor(entry.Id);
   for(int i=0;i<Directions.Length;i++) {
    row[i]=new CheckCell{Bounds=new Rectangle(colX+i*colW,top,colW,rowH),Checked=allowed.Contains(Directions[i])};
    rows.Controls.Add(row[i]);
   }
   cells[entry.Id]=row;
  }
  card.Controls.Add(rows);card.Controls.Add(header);rows.BringToFront();
  Controls.Add(card);
  y+=card.Height+Ui.Px(20);
  var standard=new ModernButton("Standard",ButtonKind.Secondary,"");
  standard.Location=new Point(pad,y);
  standard.Click+=(s,e)=>ResetToDefaults();
  var ok=new ModernButton("Speichern",ButtonKind.Primary,null);
  var cancel=new ModernButton("Abbrechen",ButtonKind.Secondary,null);
  cancel.DialogResult=DialogResult.Cancel;
  cancel.Location=new Point(w-pad-cancel.Width,y);
  ok.Location=new Point(cancel.Left-Ui.Px(8)-ok.Width,y);
  ok.Click+=(s,e)=>Accept();
  Controls.Add(standard);Controls.Add(ok);Controls.Add(cancel);
  AcceptButton=ok;CancelButton=cancel;
  y+=Ui.Px(38)+Ui.Px(20);
  ClientSize=new Size(w,y);
 }
 void ResetToDefaults() {
  foreach(var entry in programs) {
   var allowed=MainForm.DefaultEdgesFor(entry.Id);
   var row=cells[entry.Id];
   for(int i=0;i<Directions.Length;i++)row[i].Checked=allowed.Contains(Directions[i]);
  }
 }
 void Accept() {
  foreach(var entry in programs) {
   var set=new HashSet<string>();
   var row=cells[entry.Id];
   for(int i=0;i<Directions.Length;i++)if(row[i].Checked)set.Add(Directions[i]);
   Rules[entry.Id]=set;
  }
  DialogResult=DialogResult.OK;
  Close();
 }
}
// Einzelnes Häkchen (wie in der Programmliste), anklickbar und per Leertaste umschaltbar.
public class CheckCell:Control {
 bool isChecked;
 public CheckCell() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
  BackColor=Ui.Surface;Cursor=Cursors.Hand;TabStop=true;
 }
 public bool Checked{get{return isChecked;}set{isChecked=value;Invalidate();}}
 protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(e.Button==MouseButtons.Left){Focus();Checked=!Checked;}}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space){Checked=!Checked;e.Handled=true;}}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  g.Clear(BackColor);
  g.SmoothingMode=SmoothingMode.AntiAlias;
  int size=Ui.Px(20);
  var box=new Rectangle((Width-size)/2,(Height-size)/2,size,size);
  ProgramList.DrawCheck(g,box,isChecked,Enabled);
  if(Focused&&ShowFocusCues)
   using(var path=Ui.Round(Rectangle.Inflate(box,Ui.Px(3),Ui.Px(3)),Ui.Px(5)))
   using(var pen=new Pen(Ui.Blend(Ui.Accent,Ui.Surface,0.3f))) g.DrawPath(pen,path);
 }
}
}
