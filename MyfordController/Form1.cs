using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;
using System.Configuration;
using Microsoft.Win32; 

/*
# cross and lead directions - facing lathe
#
#          +
#          +
#     ----- ++++++
#          -
#          -
*/

namespace MyfordController
{    
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
        }
        string MessageFromDue;
        string ResponseFromDue;
        string MessageToDue;
        string portDue;
        Boolean running = false;
        Queue<string> strCommand = new Queue<string>();      
        Queue<string> strFeedback = new Queue<string>();
        Queue<int> strDups = new Queue<int>();        
        int totalDups;


        private void Form1_Load(object sender, EventArgs e)
        {


           
            Globals.CurrentUserKey = Registry.CurrentUser;
            Globals.SoftwareKey = Globals.CurrentUserKey.CreateSubKey("Software");
            Globals.MyfordKey = Globals.SoftwareKey.CreateSubKey("Myford");
            Globals.ManualKey = Globals.MyfordKey.CreateSubKey("Manual");
            Globals.CylinderKey = Globals.MyfordKey.CreateSubKey("Cylinder");
            Globals.PartingKey = Globals.MyfordKey.CreateSubKey("Parting");
            Globals.PeckKey = Globals.MyfordKey.CreateSubKey("Peck");
            Globals.ArcKey = Globals.MyfordKey.CreateSubKey("Arc");
            Globals.ConfigKey = Globals.MyfordKey.CreateSubKey("Config");


            Globals.HomeXmm = Convert.ToSingle(Globals.ConfigKey.GetValue("HomeXmm"));
            Globals.XOffsetmm = Convert.ToSingle(Globals.ConfigKey.GetValue("XOffsetmm"));
           // MessageBox.Show("XOffset="+ Globals.XOffsetmm);
            Globals.XRadius = Convert.ToSingle(Globals.ConfigKey.GetValue("XRadius"));
            Globals.ZOffsetmm = Convert.ToSingle(Globals.ConfigKey.GetValue("ZOffsetmm"));
            Globals.PartingToolXDistance = Convert.ToSingle(Globals.ConfigKey.GetValue("PartingToolXDistance"));
            Globals.PartingToolZDistance = Convert.ToSingle(Globals.ConfigKey.GetValue("PartingToolZDistance"));
            Globals.PartingToolWidthmm = Convert.ToSingle(Globals.ConfigKey.GetValue("PartingToolWidthmm"));
            textPlugins.Text=Convert.ToString(Globals.ConfigKey.GetValue("Plugins"));
            Globals.ConfigKey.SetValue("Plugins", textPlugins.Text);
            Globals.LAccel = Convert.ToSingle(Globals.ConfigKey.GetValue("AccelLead"));
            Globals.CAccel = Convert.ToSingle(Globals.ConfigKey.GetValue("AccelCross"));
            textLAccel.Text = Convert.ToString(Globals.ConfigKey.GetValue("AccelLead"));
            textCAccel.Text = Convert.ToString(Globals.ConfigKey.GetValue("AccelCross"));
            //aKey = ManualKey.OpenSubKey(comboManual.Text);
            //textManual.Text = (string)aKey.GetValue("");

            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                listPorts.Items.Add(port);
                portDue = port;
            }

            if (ports.GetLength(0) != 1)
            {
                MessageBox.Show("Did not find one and only one port - select a port on Configure tab before using");
                labelPort.Text = "N/A";
            }
            else
            {
                labelPort.Text = portDue;
                serialPort1.PortName = portDue;
                serialPort1.BaudRate = 115200;               
                serialPort1.DtrEnable = true;
                serialPort1.Open();
                MessageBox.Show("Opened port "+portDue);
            }
            
            comboManual.Items.Clear();
            string[] ManualKeys = Globals.ManualKey.GetSubKeyNames();           
            foreach (string thisKey in ManualKeys)
            {            
                comboManual.Items.Add(thisKey);
                comboManual.SelectedIndex = 0;
            }
            string[] CylinderKeys = Globals.CylinderKey.GetSubKeyNames();
            foreach (string thisKey in CylinderKeys)
            {
                comboCyl.Items.Add(thisKey);
                comboCyl.SelectedIndex = 0;
            }
            string[] PartingKeys = Globals.PartingKey.GetSubKeyNames();
            foreach (string thisKey in PartingKeys)
            {
                comboPart.Items.Add(thisKey);
                comboPart.SelectedIndex = 0;
            }
            string[] PeckKeys = Globals.PeckKey.GetSubKeyNames();
            foreach (string thisKey in PeckKeys)
            {
                comboPeck.Items.Add(thisKey);
                comboPeck.SelectedIndex = 0;
            }
            string[] ArcKeys = Globals.ArcKey.GetSubKeyNames();
            foreach (string thisKey in ArcKeys)
            {
                comboArcName.Items.Add(thisKey);
                comboArcName.SelectedIndex = 0;
            }


            comboCylLR.SelectedIndex = 1;
            comboCylWait.SelectedIndex =1;
            comboCylSpecific.SelectedIndex = 0;
            comboPartSpecific.SelectedIndex = 0;

            QueueCommand("Q0;#", 1, "Feedback");
            QueueCommand("X" + Convert.ToString(Globals.XOffsetmm * Globals.crossfactor) + ";#", 1,"Initialise X");
            QueueCommand("Z" + Convert.ToString(Globals.ZOffsetmm * Globals.leadfactor) + ";#", 1,"Initialise Z");
            QueueCommand("A1;F" + Convert.ToString(Globals.LAccel) + ";#", 1, "Leadscrew acceleration");
            QueueCommand("A2;F" + Convert.ToString(Globals.CAccel) + ";#", 1, "Crosslide acceleration");
            QueueCommand("Q1;#", 1, "Quiet mode");
            updateLEDS(Globals.XOffsetmm, Globals.ZOffsetmm);
                          
            checkBox1.Enabled = true;
            labelStep.Text = "Ready";            
        }

        private void Form1_FormCLosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort1.IsOpen) {
                MessageBox.Show("Closing "+portDue);
                serialPort1.Close();
            }

        }

        private String Crossmm(float CutDepthmm, string Speed,int dups)
        {
            float val;
         
            val=Convert.ToSingle(Math.Round(CutDepthmm * Globals.crossfactor));
            if (val < 0.0F)
            {
                Globals.XOffsetmm = Globals.XOffsetmm + (CutDepthmm * dups);     
                return "C" + Convert.ToString(val) + ";S-" + Speed + ";";
            }
            if (val > 0.0F)
            {
                Globals.XOffsetmm = Globals.XOffsetmm + (CutDepthmm * dups);     
                return "C" + Convert.ToString(val) + ";S" + Speed + ";";
            }
            return "C0;S1000;";
        }

        private String Leadmm(float Lengthmm, string Speed, int dups)
        {
            float val;
       
            val = Convert.ToSingle(Math.Round(Lengthmm * Globals.leadfactor));
            if (val < 0.0F)
            {
                Globals.ZOffsetmm = Globals.ZOffsetmm + (Lengthmm * dups);                
                return "L" + Convert.ToString(val) + ";S-" + Speed + ";";
            }
            if (val > 0.0F)
            {
                Globals.ZOffsetmm = Globals.ZOffsetmm + (Lengthmm  * dups);               
                return "L" + Convert.ToString(val) + ";S" + Speed + ";";
            }
            return "L0;S1000;";
        }
        
        private String Bothmm(float LeadLengthmm, string SpeedZ,string SpeedX, int dups)
        {
            float val;
            float valX;
            float valZ;
            float CrossXmm;
            //MessageBox.Show(":" + LeadLengthmm+":"+ SpeedZ + ":" + SpeedX + ":");
            

            val = Convert.ToSingle(Math.Round(LeadLengthmm * Globals.leadfactor));
            valZ = Convert.ToSingle(SpeedZ); // can be either sign - steps lead
            valX = Convert.ToSingle(SpeedX); // can be either sign - steps cross
            CrossXmm = LeadLengthmm * (valX / valZ) / Globals.leadtocrossfactor;            
            if (valX<0.0F && CrossXmm>0.0F) {
                CrossXmm=CrossXmm*-1.0F;
            }
            if (valX>0.0F && CrossXmm<0.0F) {
                CrossXmm=CrossXmm*-1.0F;
            }
           // MessageBox.Show(":" + val + ":" + valZ + ":" + valZ + ":" + CrossXmm+":"+dups);
            // CrossXmm will be negative if moving out, so will update XOffset correctly below just by adding

            if (valZ < 0.0F)
            {
                Globals.ZOffsetmm = Globals.ZOffsetmm - (LeadLengthmm * dups);                
                Globals.XOffsetmm = Globals.XOffsetmm + (CrossXmm * dups);                
                return "B-" + Convert.ToString(val) + ";S" + SpeedZ + ";S" + SpeedX + ";";
            }
            if (valZ > 0.0F)
            {
                Globals.ZOffsetmm = Globals.ZOffsetmm + (LeadLengthmm * dups);                
                Globals.XOffsetmm = Globals.XOffsetmm + (CrossXmm * dups);
                return "B" + Convert.ToString(val) + ";S" + SpeedZ + ";S" + SpeedX + ";";
            }
            return "B0;S1000;S1000;";
        }


        private void runCommand()
        {            
            string Command;            
            int l;
            string StepText;
            if (running == true)
            {
                return;
            }         
            if (strCommand.Count>0)
            {
                //MessageBox.Show("comm count=" + strCommand.Count);                
                //MessageBox.Show("dups count=" + strDups.Count);
                //MessageBox.Show("feed count=" + strFeedback.Count);
                running = true;
                labelStep.Text = "Busy";
                buttonState(false);
                Command = strCommand.Dequeue();
                StepText = strFeedback.Dequeue();
                if (StepText != "")
                {
                    labelStep.Text = StepText;
                }
                
                totalDups = Convert.ToInt16(strDups.Dequeue());                

               // if (Feedback != "")
                //{
                //    labelCommand.Text = Feedback;
               // }                                
                l=Command.Length;      
               // write command out in chunks of 64, buffer is limited to this
               // hopefully the due takes the data quickly enough
                while (l>0) {
                    if (l < 64)
                    {
                        //MessageBox.Show(Command);
                        serialPort1.Write(Command);
                        l = 0;
                    }
                    else
                    {                       
                        serialPort1.Write(Command.Substring(0, 64));
                        Command = Command.Substring(64);
                        l = l - 64;
                    }
                }
                
            }
            else {
                Console.Beep(3000, 500);
            }
        }


        public void QueueCommand(string comm, int dups,string feedback)
        {
            // must have # at end of comm
            //MessageBox.Show("Queuing " + comm + ":"+ msg + ":" + Convert.ToString(dups)+":"+feedback);
           // MessageBox.Show(comm);
            if (comm == "#")
            {
                return;
            }
            textDebug.Text = textDebug.Text + "Sending:" + comm + System.Environment.NewLine;           
            strCommand.Enqueue(comm);            
            strDups.Enqueue(dups);            
            strFeedback.Enqueue(feedback);            
            runCommand();
        }



        private void tabPage1_Click(object sender, EventArgs e)
        {

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void tabConfigure_Click(object sender, EventArgs e)
        {

        }



        private void tabManual_Click(object sender, EventArgs e)
        {

        }
        private void buttonState(Boolean state)
        {
         foreach (var b in GetSelfAndChildrenRecursive(this).OfType<Button>())
            {                
                if (b.Name != "buttonKill")
                    b.Enabled = state;
            }                            
        }

        public IEnumerable<Control> GetSelfAndChildrenRecursive(Control parent)
        {
            List<Control> controls = new List<Control>();

            foreach (Control child in parent.Controls)
            {
                controls.AddRange(GetSelfAndChildrenRecursive(child));
            }

            controls.Add(parent);

            return controls;
        }

        private void buttonKill_Click(object sender, EventArgs e)
        {
            strCommand.Clear();
            strDups.Clear();
            strFeedback.Clear();      
            buttonState (false);
            running = true;            
            labelCommand.Text = "";                    
            labelStatus.Text = "Killing";
            labelStep.Text = "Killing";
            textDebug.Text = textDebug.Text + "Sending:" + "K;#" + System.Environment.NewLine;            
            serialPort1.Write("K;#");
            labelStep.Text = "Killed";               
        }

        private void queueAny(string msg)
        {
            if (MessageToDue.Length != 0)
            {                
                MessageToDue += "#";
                QueueCommand(MessageToDue, 1,msg);                
                MessageToDue = "";
                totalDups = 1;
            }
        }


        private void buttonManual_Click(object sender, EventArgs e)
        {

            float val;
           buttonState(false);                       
           MessageToDue = "";
           textDebug.Text = "";
           textRuby.Text = "";
           totalDups = 1;
           string StepText;

            string[] tempArray = textManual.Lines;
            char[] delimiterChars = { ' '};
            StepText = "";
            labelStep.Text = "Busy";
            for (int i = 0; i < tempArray.Length; i++)
            {
                string[] pieces = tempArray[i].Split(delimiterChars);

                // validate input!!!!!!!!!!!!!!

                /*if (pieces[0]=="W")
                    if (pieces.Length != 2)
                    {
                        MessageBox.Show("Invalid command " + pieces[0] + " - needs 1 value, either 0 for button, or milliseconds");
                        return;
                    }
                if (pieces[0] == "L" || pieces[0] == "R" || pieces[0] == "I" || pieces[0] == "O")
                        if (pieces.Length!=3) {
                             MessageBox.Show("Invalid command "+pieces[0]+" - needs 2 values, distance in mm and steps/second ");
                            return;
                        }
                List<string> values3 = new List<string>() { "LI", "LO", "RI", "RO" };
                if (values3.Contains(pieces[0]) == true)
                    if (pieces.Length != 4)
                    {
                        MessageBox.Show("Invalid command " + pieces[0] + " - needs 3 values, distance in mm, steps/sec for 1st command character and steps/second for 2nd");
                        return;
                    }
                 */
                //MessageBox.Show("ZOffset="+Convert.ToString(Globals.ZOffsetmm));                
                switch (pieces[0]) {
                    case "--":
                        break;
                    case "Step":
                        queueAny(StepText);
                        StepText = pieces[1];
                        break;
                     // duplicate
                      case "D":
                        QueueCommand(MessageToDue+"#", 1,""); 
                        MessageToDue = "D" + pieces[1] + ";";
                        totalDups = Convert.ToInt16(pieces[1]);
                        break;
                        // basic, also absolute
                    case "L":
                        MessageToDue += Leadmm(Convert.ToSingle(pieces[1])*-1.0F, pieces[2],totalDups);
                        break;
                    case "R":
                        MessageToDue += Leadmm(Convert.ToSingle(pieces[1]), pieces[2], totalDups);
                        break;
                    case "I":
                        MessageToDue += Crossmm(Convert.ToSingle(pieces[1]), pieces[2], totalDups);
                        break;
                    case "O":
                        MessageToDue += Crossmm(Convert.ToSingle(pieces[1]) * -1.0F, pieces[2], totalDups);
                        break;
                    // compound
                    case "LI":
                        MessageToDue += Bothmm(Convert.ToSingle(pieces[1]), "-"+pieces[2], pieces[3], totalDups);
                        //MessageToDue += "B-" + Math.Round(Convert.ToSingle(pieces[1]) * Globals.leadfactor);
                        //MessageToDue += ";S-" + pieces[2] + ";S" + pieces[3] + ";";
                        break;
                    case "LO":
                        MessageToDue += Bothmm(Convert.ToSingle(pieces[1]), "-"+pieces[2], "-"+pieces[3], totalDups);
                        //MessageToDue += "B-" + Math.Round(Convert.ToSingle(pieces[1]) * Globals.leadfactor);
                        //MessageToDue += ";S-" + pieces[2] + ";S-" + pieces[3] + ";";
                        break;
                    case "RI":
                        MessageToDue += Bothmm(Convert.ToSingle(pieces[1]), pieces[2], pieces[3], totalDups);
                        //MessageToDue += "B" + Math.Round(Convert.ToSingle(pieces[1]) * Globals.leadfactor);
                        //MessageToDue += ";S" + pieces[2] + ";S" + pieces[3] + ";";
                        break;
                    case "RO":
                        MessageToDue += Bothmm(Convert.ToSingle(pieces[1]), pieces[2], "-"+pieces[3], totalDups);
                        //MessageToDue += "B" + Math.Round(Convert.ToSingle(pieces[1]) * Globals.leadfactor);
                        //MessageToDue += ";S" + pieces[2] + ";S-" + pieces[3] + ";";
                        break;
                    case "#":
                        queueAny(StepText);
                        break;
                    // wait
                    case "W":
                        MessageToDue += "W" + pieces[1] + ";";
                        break;

                    case "X-": // out from centre line
                        queueAny("Line " + (i + 1));
                        val = (Globals.XOffsetmm * -1.0F) - Convert.ToSingle(pieces[1]);
                        MessageToDue += Crossmm(val, pieces[2], 1);
                        //MessageBox.Show(MessageToDue);
                        //Globals.XOffsetmm = Globals.XOffsetmm + val;
                        break;
                    case "X+": // in from centre line - beyond centre
                        queueAny(StepText);
                        val = (Globals.XOffsetmm * -1.0F) + Convert.ToSingle(pieces[1]); 
                        MessageToDue += Crossmm(val, pieces[2], 1);
                        //Globals.XOffsetmm = Globals.XOffsetmm + val;
                        break;
                    // parting X absolute
                    case "PX-": // parting, out from centre line
                        queueAny(StepText);
                        val = (Globals.XOffsetmm * -1.0F) - Convert.ToSingle(pieces[1]) - Globals.PartingToolXDistance;
                        MessageToDue += Crossmm(val, pieces[2],1);
                        //Globals.XOffsetmm = Globals.XOffsetmm + val;
                        break;
                    case "PX+": // parting, in from centre line - beyond centre
                        queueAny(StepText);
                        //MessageBox.Show(":" + Globals.XOffsetmm + ":" + Convert.ToSingle(pieces[1]) + ":" + Globals.PartingToolXDistance);
                        val = (Globals.XOffsetmm * -1.0F) + Convert.ToSingle(pieces[1]) - Globals.PartingToolXDistance;
                        MessageToDue += Crossmm(val, pieces[2], 1);
                        //Globals.XOffsetmm = Globals.XOffsetmm + val;
                        break;
                    case "Z-": // go left of Z0    
                        queueAny(StepText);
                        val = (Globals.ZOffsetmm * -1.0F)  - Convert.ToSingle(pieces[1]);
                        MessageToDue += Leadmm(val, pieces[2], 1);
                        //Globals.ZOffsetmm = Globals.ZOffsetmm + val;
                        break;
                    case "Z+": // go right of Z0
                        queueAny(StepText);
                        val = (Globals.ZOffsetmm * -1.0F) + Convert.ToSingle(pieces[1]);
                        MessageToDue += Leadmm(val, pieces[2], 1);
                        //Globals.ZOffsetmm = Globals.ZOffsetmm + val;
                        break;
                    // parting Z absolute
                    case "PZ-": // parting go left of Z0                      
                        queueAny(StepText);
                        val = (Globals.ZOffsetmm * -1.0F)  - Convert.ToSingle(pieces[1])  - Globals.PartingToolZDistance;
                        MessageToDue += Leadmm(val, pieces[2], 1);
                        //Globals.ZOffsetmm = Globals.ZOffsetmm + val;
                        break;
                    case "PZ+": // parting go right of Z0
                        queueAny(StepText);
                        val = (Globals.ZOffsetmm * -1.0F) + Convert.ToSingle(pieces[1]) - Globals.PartingToolZDistance;
                        MessageToDue += Leadmm(val, pieces[2], 1);
                        //Globals.ZOffsetmm = Globals.ZOffsetmm + val;
                        break;

                    case "Q":
                        queueAny(StepText);
                        MessageToDue += "Q " + pieces[1] + ";";
                        break;

                    // set new offsets
                    case "X":
                        queueAny(StepText);
                        MessageToDue += "X" + Math.Round(Convert.ToSingle(pieces[1]) * Globals.crossfactor)+";";
                        break;
                    case "Z":
                        queueAny(StepText);
                        MessageToDue += "Z" + Math.Round(Convert.ToSingle(pieces[1]) * Globals.leadfactor) + ";";
                        break;
                    // programs
                    case "Cylinder":
                        queueAny(StepText);
                       // MessageBox.Show(pieces[1] + pieces[2]);
                        getSavedCyl(pieces[1]);
                        turnCylinder(
                               pieces[2], // req
                               textCylFinishingCuts.Text,
                               textCylFinalPasses.Text,
                               textCylFinishingmm.Text,
                               textCylRoughingmm.Text,
                               pieces[3], // length
                               textCylSpeed.Text,
                               textCylSpeedFinish.Text,
                               textCylReturnSpeed.Text,             
                               comboCylWait.Text,
                               pieces[4]); // LR                      
                        break;                    
                        case "Part":
                        queueAny(StepText);
                          getSavedPart(pieces[1]);
                          turnParting(
                                 pieces[2], // req
                                 textPartCutDepthmm.Text,
                                 textPartSpeedRelieve.Text,
                                 textPartSpeedCut.Text,
                                 pieces[4], // LRC
                                 pieces[3], // # of cuts
                                 textPartCutsHorizontalBudgemm.Text);                                 
                          break;
                        case "Peck":
                          queueAny(StepText);
                          getSavedPeck(pieces[1]);

                          turnPeck(
                                 pieces[2], // Totalmm
                                 textPeckmm.Text,
                                 textPeckSpeed.Text,
                                 textRetractmm.Text,
                                 textRetractSpeed.Text);
                          break;
                        case "Arc":
                          queueAny(StepText);
                          getSavedArc(pieces[1]);
                          string[] ArcLHS=new string[0];
                          string[] ArcRHS = new string[0];
                          turnArc(textArc.Lines,
                                 pieces[2], // speed                                 
                                 ref ArcLHS,ref ArcRHS);
                          if (pieces[3] == "L")
                          {
                              for (int arc_i = 0; arc_i < ArcLHS.Length; arc_i++)
                              {
                                  string[] LHSpieces = ArcLHS[arc_i].Split(delimiterChars);
                                  MessageToDue += Bothmm(Convert.ToSingle(LHSpieces[1]), "-" + LHSpieces[2], LHSpieces[3], totalDups);
                              }
                          }
                          else {
                              for (int arc_i = 0; arc_i < ArcRHS.Length; arc_i++)
                                 {
                                 string[] RHSpieces = ArcRHS[arc_i].Split(delimiterChars);
                                 MessageToDue += Bothmm(Convert.ToSingle(RHSpieces[1]), RHSpieces[2], RHSpieces[3], totalDups);
                                 }
                              }
                          break;


                    // invalid
                    default:
                        MessageBox.Show("Invalid command on line "+(i+1));
                        return;                        
                    }
               // MessageToDue += "Q0;";
                           
            
            }
            if (MessageToDue != "")
            {
                MessageToDue += "#";
                QueueCommand(MessageToDue, 1, StepText); 
            }
        }

        private void updateLEDS(float x,float z)
        {
            if (x < 0)
            {
                labelX.Text = ("-" + Convert.ToString(Math.Abs(Math.Round(x, 2)))).PadLeft(7, ' ');
            }
            else
            {
                labelX.Text = ("+" + Convert.ToString(Math.Abs(Math.Round(x, 2)))).PadLeft(7, ' ');
            }

            if ((x + Globals.PartingToolXDistance) < 0)
            {
                labelXP.Text = ("-" + Convert.ToString(Math.Abs(Math.Round(x + Globals.PartingToolXDistance, 2)))).PadLeft(7, ' ');
            }
            else
            {
                labelXP.Text = ("+" + Convert.ToString(Math.Abs(Math.Round(x + Globals.PartingToolXDistance, 2)))).PadLeft(7, ' ');
            }


            if (z < 0)
            {
                labelZ.Text = ("-" + Convert.ToString(Math.Abs(Math.Round(z, 2)))).PadLeft(7, ' ');
            }
            else
            {
                labelZ.Text = ("+" + Convert.ToString(Math.Abs(Math.Round(z, 2)))).PadLeft(7, ' ');
            }

            if ((z + Globals.PartingToolZDistance) < 0)
            {
                labelZP.Text = ("-" + Convert.ToString(Math.Abs(Math.Round(z + Globals.PartingToolZDistance, 2)))).PadLeft(7, ' ');
            }
            else
            {
                labelZP.Text = ("+" + Convert.ToString(Math.Abs(Math.Round(z + Globals.PartingToolZDistance, 2)))).PadLeft(7, ' ');
            }

        }


        private void GotMessageFromDue(object sender, EventArgs e)        
        {
            int n;
            float XposSteps;                               
            float ZposSteps;            
            char[] delimiterChars = { ' '};
            n = MessageFromDue.IndexOf("#");
            //MessageBox.Show(MessageFromDue);
            while (n>0)  {
                ResponseFromDue = MessageFromDue.Substring(0, n);

                string [] ResponsePieces     = ResponseFromDue.Split(delimiterChars);
                if (ResponsePieces[0] == "P")
                {
                    // MessageBox.Show(ResponseFromDue);     

                    XposSteps = Convert.ToSingle(ResponsePieces[1]);
                    Globals.XOffsetmm = XposSteps / Globals.crossfactor;
                    ZposSteps = Convert.ToSingle(ResponsePieces[2]);
                    Globals.ZOffsetmm = ZposSteps / Globals.leadfactor;

                    Globals.ConfigKey.SetValue("ZOffsetmm", Globals.ZOffsetmm);
                    Globals.ConfigKey.SetValue("XOffsetmm", Globals.XOffsetmm);
                    updateLEDS(Globals.XOffsetmm,Globals.ZOffsetmm);
                    textDebug3.Text = textDebug3.Text + ResponseFromDue + System.Environment.NewLine;
                }
                else
                {
                    textDebug2.Text = textDebug2.Text + ResponseFromDue + System.Environment.NewLine;
                }
                
                if (ResponsePieces[0] == "JL")
                {
                    labelStatus.Text = "Joystick left";
                }
                
                if (ResponsePieces[0] == "JR")
                {
                    labelStatus.Text = "Joystick right";
                }
                if (ResponsePieces[0] == "JI")
                {
                    labelStatus.Text = "Joystick in";
                }
                if (ResponsePieces[0] == "JO")
                {
                    labelStatus.Text = "Joystick out";
                }


                if (ResponsePieces[0] == "D")
                {
                    labelStatus.Text = ResponsePieces[1] + " of " + totalDups;
                }
                if (ResponsePieces[0] == "CC") // one command piece complete, may be many
                {
                    labelStatus.Text = "Command complete";
                }
                if (ResponsePieces[0] == "WT")
                {
                    labelStatus.Text = "Wait for " + ResponsePieces[1] + " milliseconds";
                }
                if (ResponsePieces[0] == "WB")
                {
                    labelStatus.Text = "Wait for button";
                }
                if (ResponsePieces[0] == "CI")
                {
                    labelStatus.Text = "In " + ResponsePieces[1]  + " steps at " + ResponsePieces[2] + " steps per second";
                }
                if (ResponsePieces[0] == "CO")
                {
                    labelStatus.Text = "Out " + ResponsePieces[1] + " steps at " + ResponsePieces[2] + " steps per second";
                }
                if (ResponsePieces[0] == "LL")
                {
                    labelStatus.Text = "Left " + ResponsePieces[1] + " steps at " + ResponsePieces[2] + " steps per second";
                }
                if (ResponsePieces[0] == "LR")
                {
                    labelStatus.Text = "Right " + ResponsePieces[1] + " steps at " + ResponsePieces[2] + " steps per second";
                }
                if (ResponsePieces[0] == "BLI")
                {
                    labelStatus.Text = "Left " + ResponsePieces[1] + " steps at "  + ResponsePieces[2] + " steps per second, in at " + ResponsePieces[3] + " steps per second" ;
                }
                if (ResponsePieces[0] == "BLO")
                {
                    labelStatus.Text = "Left " + ResponsePieces[1] + " steps at " + ResponsePieces[2] + " steps per second, out at " + ResponsePieces[3] + " steps per second";
                }
                if (ResponsePieces[0] == "BRI")
                {
                    labelStatus.Text = "Right " + ResponsePieces[1] + " steps at  " + ResponsePieces[2] + " steps per second, in at " + ResponsePieces[3] + " steps per second";
                }
                if (ResponsePieces[0] == "BRO")
                {
                    labelStatus.Text = "Right " + ResponsePieces[1] + " steps at " + ResponsePieces[2] + " steps per second, out at " + ResponsePieces[3] + " steps per second";
                }
                if (ResponsePieces[0] == "PC") // Sequence complete, all commands done
                {
                    Globals.ConfigKey.SetValue("ZOffsetmm", Globals.ZOffsetmm);
                    Globals.ConfigKey.SetValue("XOffsetmm", Globals.XOffsetmm);                    
                    updateLEDS(Globals.XOffsetmm, Globals.ZOffsetmm);
                    
                    running = false;
                    buttonState(true);
                    checkBox1.Enabled = true;
                    labelStatus.Text = "";
                    labelStep.Text = "Ready";
                    runCommand();              
                }                                         
                MessageFromDue = MessageFromDue.Substring(n+1);
                n = MessageFromDue.IndexOf("#");
            }
                ResponseFromDue = "";          
         }
            
 
        private void serialPort1_DataReceived_1(object sender, SerialDataReceivedEventArgs e)
        {
            
            MessageFromDue += serialPort1.ReadExisting();
            
            this.Invoke(new EventHandler(GotMessageFromDue));


        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void labelStatus_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }


        private void label6_Click(object sender, EventArgs e)
        {

        }

        private void numericDups_ValueChanged(object sender, EventArgs e)
        {

        }

        private void label8_Click(object sender, EventArgs e)
        {

        }

        private void label10_Click(object sender, EventArgs e)
        {

        }

        private void tabCyl_Click(object sender, EventArgs e)
        {

        }

        private void buttonCutOnce_Click(object sender, EventArgs e)
        {
           float CutDepthmm;
           float Lengthmm;
           CutDepthmm=Convert.ToSingle(textCylRoughingmm.Text);           
           Lengthmm=Convert.ToSingle(textCylLengthmm.Text);  
           if (comboCylLR.Text=="Right") {
           Lengthmm=Lengthmm*-1.0F;    
               }
           QueueCommand(Crossmm(CutDepthmm, "500", 1) + "#", 1, "Cut once");
           QueueCommand(Leadmm(Lengthmm, textCylSpeed.Text, 1) + "#",  1, "Cut once");
           QueueCommand(Crossmm(-1, "500", 1) + "#", 1, "Cut once");
           QueueCommand(Leadmm(Lengthmm * -1.0F, textCylReturnSpeed.Text, 1) + "#",1, "Cut once");
           QueueCommand(Crossmm(1, "500", 1) + "#", 1, "Cut once");
           labelStatus.Text = "Cut once";
           runCommand(); 
        }

        private void textRoughing_TextChanged(object sender, EventArgs e)
        {
        
        }

        private void textCode_TextChanged(object sender, EventArgs e)
        {

        }




        public void turnCylinder(
                                  string strRequiredDiameter,
                                  string strFinishingCuts,
                                  string strFinalPasses,
                                  string strFinishing,
                                  string strRoughing,
                                  string strLength,
                                  string strSpeed,
                                  string strSpeedFinish, 
                                  string strBoxCylReturn,                                  
                                  string strCylWait,
                                  string strLR)
        {
            float CurrDiameter;
            float ReqDiameter;
            float mmInReq;
            int RoughStepsTotal;
            int RoughStepsPerCut;
            int RoughInitialCuts;
            float RoughInitialCutmm;
            float RoughFinalCutmm;
            int RoughFinalSteps;
            int FinishCuts;
            int FinishStepsPerCut;
            int FinishStepsTotal;
            int FinalPasses;
            float FinishCutmm;
            float Lengthmm;

            int TotalSteps;
            string CylWait;
           
            CurrDiameter = Globals.XOffsetmm * -2.0F ;
            
            ReqDiameter = Convert.ToSingle(strRequiredDiameter);
            FinishCuts = Convert.ToInt16(strFinishingCuts);
            FinalPasses = 0;
            if (textCylFinalPasses.Text!= "") {
               FinalPasses = Convert.ToInt16(strFinalPasses);
            }
            mmInReq = (CurrDiameter - ReqDiameter) / 2.0F;

            TotalSteps = Convert.ToInt16(Math.Round(mmInReq * Globals.crossfactor));
            FinishStepsPerCut = Convert.ToInt16(Math.Round(Convert.ToSingle(strFinishing) * Globals.crossfactor));
            FinishStepsTotal = FinishStepsPerCut * FinishCuts;
            RoughStepsTotal = TotalSteps - FinishStepsTotal;
            RoughStepsPerCut = Convert.ToInt16(Math.Round(Convert.ToSingle(strRoughing) * Globals.crossfactor));
            RoughInitialCuts = RoughStepsTotal / RoughStepsPerCut;
            RoughFinalSteps = RoughStepsTotal - (RoughStepsPerCut * RoughInitialCuts);
            RoughInitialCutmm = RoughStepsPerCut / Globals.crossfactor;
            RoughFinalCutmm = RoughFinalSteps / Globals.crossfactor;
            FinishCutmm = FinishStepsPerCut / Globals.crossfactor;

            if ((TotalSteps / FinishStepsPerCut) < FinishCuts)
            {
                MessageBox.Show("Not enough metal for finishing cuts, reduce #cuts or depth:"+TotalSteps+":"+FinishStepsPerCut+":"+FinishCuts);
                return;
            }

            if (strCylWait == "Yes")
            {
                CylWait = "W0;";
            }
            else
            {
                CylWait = "";
            }
            Lengthmm = Convert.ToSingle(strLength);
                
            if (strLR == "R")
            {
                Lengthmm = Lengthmm * -1.0F;                
            }

            // rough initial
            QueueCommand("D" + RoughInitialCuts + ";" +
                   Crossmm(RoughInitialCutmm,"1500",RoughInitialCuts)+ 
                   Leadmm(Lengthmm,strSpeed,RoughInitialCuts) + 
                   Crossmm(-1,"1500",RoughInitialCuts)+
                   Leadmm(Lengthmm * -1.0F, strBoxCylReturn, RoughInitialCuts) +
                   Crossmm(1, "500", RoughInitialCuts) +
                    CylWait + "#",  RoughInitialCuts, "");

            // rough final
            QueueCommand(
                    Crossmm(RoughFinalCutmm, "1500", 1) +
                   Leadmm(Lengthmm, strSpeed, 1) +
                   Crossmm(-1, "1500", 1) +
                   Leadmm(Lengthmm *-1.0F, strBoxCylReturn,1) +
                   Crossmm(1, "500", 1) +
                   CylWait + "#",  1, "");

            /// finishing

            QueueCommand("D" + textCylFinishingCuts.Text + ";" +
                    Crossmm(FinishCutmm, "1500", FinishCuts) +
                   Leadmm(Lengthmm, strSpeedFinish, FinishCuts) +
                   Crossmm(-1, "1500", FinishCuts) +
                   Leadmm(Lengthmm * -1.0F, strBoxCylReturn, FinishCuts) +
                   Crossmm(1, "500", FinishCuts) +
                   CylWait + "#",  1, "");

            // final

            if (textCylFinalPasses.Text!= "") {
                if (FinalPasses != 0)
                {
                    QueueCommand('D' + textCylFinalPasses.Text + ';' +
                       Leadmm(Lengthmm, strSpeedFinish, FinalPasses) +
                       Leadmm(Lengthmm * -1.0F, strSpeedFinish, FinalPasses) +
                       CylWait + "#", FinalPasses, "");
                }
            }
            runCommand();

        }

        private void buttonRunCylinder_Click(object sender, EventArgs e)
        {
           
            turnCylinder(
             textCylRequiredDiametermm.Text,
             textCylFinishingCuts.Text,
             textCylFinalPasses.Text,
             textCylFinishingmm.Text,
             textCylRoughingmm.Text,
             textCylLengthmm.Text,
             textCylSpeed.Text,
             textCylSpeedFinish.Text,
             textCylReturnSpeed.Text,             
             comboCylWait.Text,
             comboCylLR.Text);
            
        }


        public void turnPeck(
            string strPeckTotalmm,
            string strPeckmm,
            string strPeckSpeed,
            string strRetractmm,
            string strRetractSpeed)
        {
            float Peckmm;
            float FinalPeckmm;
            float TotalDepthmm;           
            float Retractmm;            
            int FullPecks;

            Peckmm = Convert.ToSingle(strPeckmm);
            Retractmm = Convert.ToSingle(strRetractmm);
            TotalDepthmm = Convert.ToSingle(strPeckTotalmm);
            FinalPeckmm = TotalDepthmm % Peckmm;
            FullPecks = Convert.ToInt16((TotalDepthmm - FinalPeckmm) / Peckmm);
           // MessageBox.Show("Pecks "+FullPecks);

            // peck away
            if (FullPecks > 0) {
                for (int i = 0; i < FullPecks; i++)
                {
                    // drill
                    QueueCommand(Leadmm(Peckmm * -1.0F, strPeckSpeed, 1) + "#", 1, "");
                    if (Retractmm == 0)
                    {
                        // retract
                        QueueCommand(Leadmm(Peckmm * (i + 1), strRetractSpeed, 1) + "#", 1, "");
                        // return to drill position
                        QueueCommand(Leadmm(Peckmm * -1.0F * (i + 1), strRetractSpeed, 1) + "#", 1, "");
                    } // Retractmm==0
                    else
                    {
                        // retract
                        QueueCommand(Leadmm(Retractmm, strRetractSpeed, 1) + "#", 1, "");                                                                              // return to drill position
                        QueueCommand(Leadmm(Retractmm * -1.0F, strRetractSpeed, 1) + "#", 1, "");
                    } // retractmm!=0
                  } // for
               } // FullPecks > 0

 
           // final peck
           if (FinalPeckmm > 0.0F) {
              // drill
              QueueCommand(Leadmm(FinalPeckmm * -1.0F, strPeckSpeed, 1) + "#",  1, "");            
              // retract   
           }   // FinalPeckmm > 0.0F
           QueueCommand(Leadmm(Peckmm * FullPecks + FinalPeckmm, strRetractSpeed, 1) + "#",  1, "");                     
           runCommand();
        } // turnPeck

        private void label18_Click(object sender, EventArgs e)
        {

        }

        private void label19_Click(object sender, EventArgs e)
        {

        }

        private void comboManualSettings_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void comboManual_SelectedIndexChanged(object sender, EventArgs e)
        {
            Globals.aKey = Globals.ManualKey.OpenSubKey(comboManual.Text);
            textManual.Text = (string)Globals.aKey.GetValue("");
            textManualSave.Text = comboManual.Text;
        }

        private void comboLR_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void label21_Click(object sender, EventArgs e)
        {

        }

        private void buttonManualSave_Click(object sender, EventArgs e)
        {
            if (checkLaptop.Checked == true)
            {
                using (StreamWriter outfile = new StreamWriter("C:/dropbox/Garth/Myford/Controller/Controller_Due/" + textManualSave.Text + ".cnc"))
                {
                    outfile.Write(textManual.Text);
                    outfile.Close();
                }
            }
            MessageBox.Show("Command saved as " + textManualSave.Text);
            Globals.aKey = Globals.ManualKey.CreateSubKey(textManualSave.Text);
            Globals.aKey.SetValue("", textManual.Text);
            comboManual.Items.Clear();
            string[] ManualKeys = Globals.ManualKey.GetSubKeyNames();
            foreach (string thisKey in ManualKeys)
            {
                comboManual.Items.Add(thisKey);                               
            }
            comboManual.Text = textManual.Text;            
        }

        private void textManualSave_TextChanged(object sender, EventArgs e)
        {

        }

        private void textManual_TextChanged(object sender, EventArgs e)
        {

        }

        private void textFinalPasses_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            Globals.ConfigKey.SetValue("ZOffsetmm", Globals.ZOffsetmm);
            Globals.ConfigKey.SetValue("XOffsetmm", Globals.XOffsetmm);
            Globals.ConfigKey.SetValue("PartingToolXDistance", Globals.PartingToolXDistance);
            Globals.ConfigKey.SetValue("PartingToolZDistance", Globals.PartingToolZDistance);
            Globals.ConfigKey.SetValue("PartingToolWidthmm", Globals.PartingToolWidthmm);
            Environment.Exit(0);
        }

        private void comboBoxCylWait_SelectedIndexChanged(object sender, EventArgs e)
        {

        }



        public void turnParting( string strRequiredDiameter,
                                 string strCutDepth,
                                 string strSpeedRelieve,
                                 string strSpeedCut,
                                 string strLRC,
                                 string strCutsHorizontal,
                                 string strCutsHorizontalmm)
        {

            float CurrDiameter;
            float ReqDiameter;
            float mmInReq;
            float CutDepthmm;
            float CutDepthFinalmm;
            int TotalSteps;
            int FullCuts;
            int StepsPerFullCut;
            int FinalCutSteps;
            int CutsHorizontal;
            float CutsHorizontalmm;
            float CutsHorizontalReturnmm;
            string strPartCommand;
            float Reliefmm = 1.0F;
            //string strFinal;
            //string strReturnToStart;            
            //MessageBox.Show("Parting starting");
            CurrDiameter = (Globals.XOffsetmm + Globals.PartingToolXDistance) * 2.0F;

            ReqDiameter = Convert.ToSingle(strRequiredDiameter);
            mmInReq = (CurrDiameter - ReqDiameter) / 2.0F;

            CutDepthmm = Convert.ToSingle(strCutDepth);
            //Reliefmm = 1.0F;
            TotalSteps = Convert.ToInt16(Math.Round(mmInReq * Globals.crossfactor));
            StepsPerFullCut = Convert.ToInt16(Math.Round(CutDepthmm * Globals.crossfactor));
            FullCuts = TotalSteps / StepsPerFullCut;

            FinalCutSteps = TotalSteps - (FullCuts * StepsPerFullCut);
            CutDepthFinalmm = FinalCutSteps / Globals.crossfactor;
            CutsHorizontal = Convert.ToInt16(strCutsHorizontal);
            CutsHorizontalmm = Convert.ToSingle(strCutsHorizontalmm);
            if (strLRC == "R")
            {
                CutsHorizontalmm = CutsHorizontalmm * -1.0F;
            }
            CutsHorizontalReturnmm = CutsHorizontalmm * (CutsHorizontal - 1) * -1.0F;

            //MessageBox.Show("Full depth cuts=" + Convert.ToString(FullCuts));
            //MessageBox.Show("Final cut mm =" + Convert.ToString(CutDepthFinalmm));
            //MessageBox.Show("mm per lead move=" + Convert.ToString(CutsHorizontalmm));
            //MessageBox.Show("mm lead return=" + Convert.ToString(CutsHorizontalReturnmm));
            // start at side of cut, not centre

            // initial cuts
            if ((strLRC == "L") || (strLRC == "R"))
            {
                strPartCommand = "D" + FullCuts + ';';
                strPartCommand = strPartCommand + Crossmm(CutDepthmm*-1.0F, strSpeedCut,FullCuts); 
                for (int i = 0; i < (CutsHorizontal - 1); i++)
                {
                    strPartCommand = strPartCommand + Crossmm(CutDepthmm+Reliefmm, strSpeedRelieve,FullCuts);
                    strPartCommand = strPartCommand + Leadmm(CutsHorizontalmm, strSpeedCut, FullCuts); // lead
                    strPartCommand = strPartCommand + Crossmm((CutDepthmm + Reliefmm) * -1.0F, strSpeedCut, FullCuts); 
                }
                strPartCommand = strPartCommand + Leadmm(CutsHorizontalReturnmm, strSpeedCut, FullCuts); // lead
                strPartCommand = strPartCommand + "#";
                QueueCommand(strPartCommand,FullCuts,"");

                // final cut
                if (CutDepthFinalmm > 0.0)
                {
                    strPartCommand = Crossmm(CutDepthFinalmm * -1.0F, strSpeedCut, 1);
                    for (int i = 0; i < (CutsHorizontal - 1); i++)
                    {
                        strPartCommand = strPartCommand + Crossmm(CutDepthFinalmm + Reliefmm, strSpeedRelieve,1);
                        strPartCommand = strPartCommand + Leadmm(CutsHorizontalmm, strSpeedCut,1); // lead
                        strPartCommand = strPartCommand + Crossmm((CutDepthFinalmm + Reliefmm) * -1.0F, strSpeedCut,1);
                    }
                    strPartCommand = strPartCommand + Leadmm(CutsHorizontalReturnmm, strSpeedCut, 1); // lead
                    strPartCommand = strPartCommand + Crossmm(CutDepthFinalmm, strSpeedRelieve,1);                    
                    //strPartCommand = strPartCommand + Crossmm((CutDepthFinalmm + Reliefmm)*-1.0F, strSpeed);
                    strPartCommand = strPartCommand + "#";
                    QueueCommand(strPartCommand,  1, "");
                }

                // move tool out to where it initially started
                // leadscrew is already at that point
                strPartCommand = Crossmm(CutDepthmm * FullCuts, strSpeedRelieve,1);
                strPartCommand = strPartCommand + "#";
                QueueCommand(strPartCommand,  1, "");
            }

            // start at centre of cut - for deeper parting
            if (strLRC == "Centre")
            {

            }
 
        }


        private void buttonParting_Click(object sender, EventArgs e)
        {
           
            turnParting(textPartRequiredDiametermm.Text,
                        textPartCutDepthmm.Text,
                        textPartSpeedRelieve.Text,
                        textPartSpeedCut.Text,
                        comboPartLRC.Text,
                        textPartCutsHorizontal.Text,
                        textPartCutsHorizontalBudgemm.Text);
        }

        private void label26_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged_1(object sender, EventArgs e)
        {

        }

        private void label27_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged_2(object sender, EventArgs e)
        {

        }

        private void comboPartingLRC_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void label30_Click(object sender, EventArgs e)
        {

        }

        private void label31_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged_3(object sender, EventArgs e)
        {

        }

        private void ButtonHomeX_Click(object sender, EventArgs e)
        {
            Globals.XRadius = Convert.ToSingle(TextConfigCurrentmm.Text) / 2.0F;
            QueueCommand("X" + Convert.ToString(Globals.XRadius * Globals.crossfactor * -1.0F) + ";#", 1, "Setting X");
            labelStatus.Text = "Setting X";
            Globals.ConfigKey.SetValue("XOffsetmm", Globals.XRadius * -1.0F);
            Globals.XOffsetmm = Globals.XRadius * -1.0F;
            Globals.ConfigKey.SetValue("XRadius", Globals.XRadius);

        }


        private void buttonConfigSetZ_Click(object sender, EventArgs e)
        {
            Globals.ToolWidth = Convert.ToSingle(textConfigToolWidth.Text);
            QueueCommand("Z" + Convert.ToString(Globals.ToolWidth * Globals.leadfactor) + ";#",1, "Setting Z");
            labelStatus.Text = "Setting Z";
            Globals.ZOffsetmm = Globals.ToolWidth;            
            Globals.ConfigKey.SetValue("ZOffsetmm", Globals.ZOffsetmm);
            Globals.ConfigKey.SetValue("ToolWidth", Globals.ToolWidth);
            runCommand();
        }

        private void buttonConfigSetX_Click(object sender, EventArgs e)
        {
        }

        private void buttonGoHomeX_Click(object sender, EventArgs e)
        {

        }

        private void label35_Click(object sender, EventArgs e)
        {

        }


        private void buttonConfigHome_Click(object sender, EventArgs e)
        {   
        }

        private void buttonDebugClear_Click(object sender, EventArgs e)
        {
            textDebug.Text = "";
            textRuby.Text = "";
        }

        private void buttonRuby_Click(object sender, EventArgs e)
        {
            string[] tempArray = textDebug.Lines;
            string valstr;
            float val;
            float val2;
            int dups;
            int first;
            float lead;
            float cross;
            string leadstr;
            string crossstr;
            char[] delimiterChars = { ';'};

            textRuby.Text = textRuby.Text + "require 'sketchup'" + System.Environment.NewLine;
            textRuby.Text = textRuby.Text + "load 'transformable_ci.rb'" + System.Environment.NewLine;
            textRuby.Text = textRuby.Text + "movable = TransformableCI.new( Sketchup.active_model().selection()[0])" + System.Environment.NewLine;
            if (checkInitialMove.Checked)
            {
                textRuby.Text = textRuby.Text + "pts = Geom::Point3d.new(-0.12/25.4,-24.3/25.4,0)" + System.Environment.NewLine;
                textRuby.Text = textRuby.Text + "movable.move pts" + System.Environment.NewLine;
                textRuby.Text = textRuby.Text + "UI.messagebox '';" + System.Environment.NewLine;
            }

            for (int lineno = 0; lineno < tempArray.Length; lineno++)
            {
                tempArray[lineno] = tempArray[lineno].Replace("Q0;", "");
                tempArray[lineno] = tempArray[lineno].Replace("Sending:", "");
                tempArray[lineno] = tempArray[lineno].Replace("#", "");

                string[] pieces = tempArray[lineno].Split(delimiterChars);
                if (pieces[0].Length==0) {
                    continue;
                }
                valstr = pieces[0].Substring(1);
                val = Convert.ToSingle(valstr);
                if (pieces[0].Substring(0, 1) == "D") {
                    dups = Convert.ToInt16(val);                                
                    first=1;
                   }
                else {
                    dups=1;
                    first=0;
                    }
                for (int duprepeat = 0; duprepeat < dups; duprepeat++)
                {
                    for (int linepiece = first; linepiece < pieces.Length; linepiece++)
                    {
                        if (pieces[linepiece].Length == 0)
                        {
                            continue;
                        }
                        if (pieces[linepiece].Substring(0, 1) == "S")
                        {
                            continue;
                        }
                        valstr = pieces[linepiece].Substring(1);
                        val = Convert.ToSingle(valstr);

                        if (pieces[linepiece].Substring(0, 1) == "B")
                        {
                            leadstr = pieces[linepiece + 1].Substring(1);
                            crossstr = pieces[linepiece + 2].Substring(1);
                            val = Convert.ToSingle(Math.Round(val / Globals.leadfactor, 2));
                            lead = Convert.ToSingle(leadstr);
                            cross = Convert.ToSingle(crossstr);

                            val2 = val / Globals.leadtocrossfactor * (cross / lead);
                                                               
                            linepiece = linepiece + 2;
                            textRuby.Text = textRuby.Text + "movable.move " + val + "/25.4," + val2 + "/25.4,0" + System.Environment.NewLine;
                            textRuby.Text = textRuby.Text + "UI.messagebox '';" + System.Environment.NewLine;
                        }

                        if (pieces[linepiece].Substring(0, 1) == "C")
                        {
                           val = Convert.ToSingle(Math.Round(val / 629.9213, 2));
                           textRuby.Text = textRuby.Text + "movable.move 0," + val + "/25.4,0" + System.Environment.NewLine;
                           textRuby.Text = textRuby.Text + "UI.messagebox '';" + System.Environment.NewLine;
                        }
                        if (pieces[linepiece].Substring(0, 1) == "L")
                        {
                            val = Convert.ToSingle(Math.Round(val / 214.1732, 2));
                            textRuby.Text = textRuby.Text + "movable.move "+val + "/25.4, 0,0" + System.Environment.NewLine;
                            textRuby.Text = textRuby.Text + "UI.messagebox '';" + System.Environment.NewLine;
                        }       
                   } // linepieces
                }  // dups             
            } // lines
            using (StreamWriter outfile = new StreamWriter(textPlugins.Text+"/myford.rb"))
            {
                outfile.Write(textRuby.Text );
                outfile.Close();
            }
            
        }

        private void panel5_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label42_Click(object sender, EventArgs e)
        {

        }

        private void label43_Click(object sender, EventArgs e)
        {

        }

        private void buttonConfigParting_Click(object sender, EventArgs e)
        {
            Globals.PartingToolWidthmm = Convert.ToSingle(textPartingWidth.Text);
            
            Globals.PartingToolXDistance = (Globals.XOffsetmm * -1.0F) + Globals.XRadius;
            Globals.PartingToolZDistance = Globals.ZOffsetmm * -1.0F;
            Globals.ConfigKey.SetValue("PartingToolWidthmm", textPartingWidth.Text);
            Globals.ConfigKey.SetValue("PartingToolXDistance", Convert.ToString(Globals.PartingToolXDistance));
            Globals.ConfigKey.SetValue("PartingToolZDistance", Convert.ToString(Globals.PartingToolZDistance));
            updateLEDS(Globals.XOffsetmm, Globals.ZOffsetmm);
            MessageBox.Show("Parting offsets set");
           // MessageBox.Show("Parting tool set to X "+ Convert.ToString(Globals.PartingToolXOffset) + " Z" + Convert.ToString(Globals.PartingToolZOffset));

        }

        private void textMoveOut_TextChanged(object sender, EventArgs e)
        {

        }

        private void label37_Click(object sender, EventArgs e)
        {

        }

        private void label34_Click(object sender, EventArgs e)
        {

        }

        private void TextConfigCurrentmm_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBoxCylReturn_TextChanged(object sender, EventArgs e)
        {

        }
        public void getSavedCyl(string SavedName)
        {
            //MessageBox.Show(SavedName);
            Globals.aKey = Globals.CylinderKey.OpenSubKey(SavedName);
            textCylRequiredDiametermm.Text = Convert.ToString(Globals.aKey.GetValue("CylRequiredDiametermm"));
            textCylFinishingCuts.Text = Convert.ToString(Globals.aKey.GetValue("CylFinishingCuts"));
            textCylFinalPasses.Text = Convert.ToString(Globals.aKey.GetValue("CylFinalPasses"));
            textCylFinishingmm.Text = Convert.ToString(Globals.aKey.GetValue("CylFinishingmm"));
            textCylRoughingmm.Text = Convert.ToString(Globals.aKey.GetValue("CylRoughingmm"));
            textCylLengthmm.Text = Convert.ToString(Globals.aKey.GetValue("CylLengthmm"));
            textCylSpeed.Text = Convert.ToString(Globals.aKey.GetValue("CylSpeed"));
            textCylSpeedFinish.Text = Convert.ToString(Globals.aKey.GetValue("CylSpeedFinish"));
            textCylReturnSpeed.Text = Convert.ToString(Globals.aKey.GetValue("CylReturnSpeed"));
            comboCylWait.Text = Convert.ToString(Globals.aKey.GetValue("comboCylWait"));
            comboCylLR.Text = Convert.ToString(Globals.aKey.GetValue("comboCylLR"));
            textCyl.Text = comboCyl.Text;             

        }
        public void getSavedPart(string SavedName)
        {
            Globals.aKey = Globals.PartingKey.OpenSubKey(SavedName);
            textPartRequiredDiametermm.Text = Convert.ToString(Globals.aKey.GetValue("PartRequiredDiametermm"));
            comboPartLRC.Text = Convert.ToString(Globals.aKey.GetValue("comboPartLRC"));
            comboPartWait.Text = Convert.ToString(Globals.aKey.GetValue("comboPartWait"));
            textPartCutDepthmm.Text = Convert.ToString(Globals.aKey.GetValue("PartCutDepthmm"));
            textPartSpeedRelieve.Text = Convert.ToString(Globals.aKey.GetValue("PartSpeed"));
            textPartSpeedCut.Text = Convert.ToString(Globals.aKey.GetValue("PartSpeedOut"));
            textPartCutsHorizontal.Text = Convert.ToString(Globals.aKey.GetValue("PartCutsHorizontal"));
            textPartCutsHorizontalBudgemm.Text = Convert.ToString(Globals.aKey.GetValue("PartCutsHorizontalBudgemm"));
            textPart.Text = comboPart.Text;             

        }

        private void comboPart_SelectedIndexChanged(object sender, EventArgs e)
        {
            getSavedPart(comboPart.Text);


        }

        private void comboCyl_SelectedIndexChanged(object sender, EventArgs e)
        {
             getSavedCyl(comboCyl.Text);
        }

        private void comboCylSpecific_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboCylSpecific.Text=="Generic")
            {               
                textCylRequiredDiametermm.Enabled = false;
                textCylLengthmm.Enabled = false;
            }
            else
            {             
                textCylRequiredDiametermm.Enabled = true;
                textCylLengthmm.Enabled = true;
            }
        }

        private void buttonSaveCyl_Click(object sender, EventArgs e)
        {
            Globals.aKey = Globals.CylinderKey.CreateSubKey(textCyl.Text);
            Globals.aKey.SetValue("CylRequiredDiametermm",textCylRequiredDiametermm.Text);
            Globals.aKey.SetValue("CylFinishingCuts",textCylFinishingCuts.Text);
            Globals.aKey.SetValue("CylFinalPasses",textCylFinalPasses.Text);
            Globals.aKey.SetValue("CylFinishingmm",textCylFinishingmm.Text);
            Globals.aKey.SetValue("CylRoughingmm",textCylRoughingmm.Text);
            Globals.aKey.SetValue("CylLengthmm",textCylLengthmm.Text);
            Globals.aKey.SetValue("CylSpeed",textCylSpeed.Text);
            Globals.aKey.SetValue("CylSpeedFinish",textCylSpeedFinish.Text);
            Globals.aKey.SetValue("CylReturnSpeed",textCylReturnSpeed.Text);
            Globals.aKey.SetValue("comboCylWait",comboCylWait.Text);
            Globals.aKey.SetValue("comboCylLR", comboCylLR.Text);

            comboCyl.Items.Clear();
            string[] CylinderKeys = Globals.CylinderKey.GetSubKeyNames();
            foreach (string thisKey in CylinderKeys)
            {
                comboCyl.Items.Add(thisKey);
                comboCyl.Text = textCyl.Text;
            }
            MessageBox.Show("Parameters saved as " + textCyl.Text);
        }

        private void comboPartSpecific_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboPartSpecific.Text == "Generic")
            {

                textPartRequiredDiametermm.Enabled = false;
                textPartCutsHorizontal.Enabled = false;
            }
            else
            {

                textPartRequiredDiametermm.Enabled = true;
                textPartCutsHorizontal.Enabled = true;
            }
        }

        private void buttonPartSave_Click(object sender, EventArgs e)
        {
            Globals.aKey = Globals.PartingKey.CreateSubKey(textPart.Text);            
            Globals.aKey.SetValue("PartRequiredDiametermm",textPartRequiredDiametermm.Text);
            Globals.aKey.SetValue("comboPartLRC",comboPartLRC.Text);
            Globals.aKey.SetValue("comboPartWait",comboPartWait.Text);
            Globals.aKey.SetValue("PartCutDepthmm",textPartCutDepthmm.Text);
            Globals.aKey.SetValue("PartSpeed",textPartSpeedRelieve.Text);
            Globals.aKey.SetValue("PartSpeedOut",textPartSpeedCut.Text);
            Globals.aKey.SetValue("PartCutsHorizontal",textPartCutsHorizontal.Text);
            Globals.aKey.SetValue("PartCutsHorizontalBudgemm",textPartCutsHorizontalBudgemm.Text);

            comboPart.Items.Clear();
            string[] PartKeys = Globals.PartingKey.GetSubKeyNames();
            foreach (string thisKey in PartKeys)
            {
                comboPart.Items.Add(thisKey);
                comboPart.Text = textPart.Text;
            }
            MessageBox.Show("Parameters saved as " + textPart.Text);

        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            textDebug2.Text = "";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            textDebug3.Text = "";
        }

        private void textPartCurrentDiametermm_TextChanged(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            textManual.Text = "";
        }

        private void label35_Click_1(object sender, EventArgs e)
        {

        }

        private void label8_Click_1(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                QueueCommand("J1;#", 1, "Joystick on");
                buttonState(false);
                buttonKill.Enabled = false;
            }
            else
            {
                QueueCommand("J0;#", 1, "Joystick off");
                buttonState(true);
                buttonKill.Enabled = true;
            }
        }

        private void checkLaptop_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void buttonCorrect_Click(object sender, EventArgs e)
        {
            float val;
            if (textXAdjust.Text != "")
            {
                MessageBox.Show("Adjusting X by " + textXAdjust.Text);
                val = Convert.ToSingle(textXAdjust.Text);
                Globals.XOffsetmm = Globals.XOffsetmm + val/2.0F;
                Globals.XRadius = Globals.XRadius + val / 2.0F;
                Globals.ConfigKey.SetValue("XOffsetmm", Globals.XOffsetmm);
                Globals.ConfigKey.SetValue("XRadius", Globals.XRadius);
                QueueCommand("X" + Convert.ToString(Globals.XRadius * Globals.crossfactor * -1.0F) + ";#", 1, "Setting X");
                updateLEDS(Globals.XOffsetmm, Globals.ZOffsetmm);
                textXAdjust.Text = "";
            }
            if (textZAdjust.Text != "")
            {
                MessageBox.Show("Adjusting Z by " + textZAdjust.Text);
                val = Convert.ToSingle(textZAdjust.Text);
                Globals.ZOffsetmm = Globals.ZOffsetmm - val;
                Globals.ConfigKey.SetValue("ZOffsetmm", Globals.ZOffsetmm);
                QueueCommand("Z" + Convert.ToString(Globals.ToolWidth * Globals.leadfactor) + ";#", 1, "Setting Z");
                updateLEDS(Globals.XOffsetmm, Globals.ZOffsetmm);
                textZAdjust.Text = "";
            }
            if (textXAdjust.Text != "")
            {
                MessageBox.Show("Not done yet! Adjusting parting Z by " + textPartZAdjust.Text);
            }
            if (textXAdjust.Text != "")
            {
                MessageBox.Show("Not done yet! Adjusting parting X by " + textPartXAdjust.Text);
            }


        }

        private void tabPen_Click(object sender, EventArgs e)
        {

        }

        private void buttonPenLoad_Click(object sender, EventArgs e)
        {
            textArc.Text="";
            var file = System.IO.File.OpenText("C:\\Dropzone\\curve.txt");
            while (!file.EndOfStream) {
               String line = file.ReadLine()+System.Environment.NewLine;               
               {                   
                   textArc.Text+=line;
               }
            }
            file.Close();
        }

        private void turnArc(string[] coords, string leadsteps, ref string[] commandsLHS,  ref string[] commandsRHS)
        {
            float prev_x;
            float prev_y;
            float x;
            float y;
            float x_diff;
            float y_diff;
            float min_y = 0;
            float leadstepspersecond;
            int y_steps;
            int min_index;
            int array_i;
            leadstepspersecond = Convert.ToSingle(leadsteps);
            char[] delimiterChars = { ' ' };
            string[] tempArray = coords;
            string[] piecesprev = tempArray[0].Split(delimiterChars);
            min_index = 0;

            array_i=0;            
            for (int i = 1; i < (tempArray.Length - 1); i++)
            {
                string[] pieces = tempArray[i].Split(delimiterChars);
                if (i > 1)
                {
                    x = Convert.ToSingle(pieces[0]);
                    y = Convert.ToSingle(pieces[1]);
                    prev_x = Convert.ToSingle(piecesprev[0]);
                    prev_y = Convert.ToSingle(piecesprev[1]);
                    if (y < min_y)
                    {
                        min_y = y;
                        min_index = i; // save off for creating LHS code - need to traverse array in reverse order
                    }
                    if (x > 0.0F)
                    {
                        x_diff = x - prev_x;
                        y_diff = (prev_y - y) * -1;
                        y_steps = Convert.ToInt32(Math.Round(Globals.leadtocrossfactor * leadstepspersecond) * y_diff / x_diff);                        
                        Array.Resize(ref commandsRHS, commandsRHS.Length + 1);
                        commandsRHS[array_i]= "RI " + x_diff + " "+leadsteps + " " + y_steps;
                        array_i++;
                    }
                }
                piecesprev = tempArray[i].Split(delimiterChars);
            }

            array_i=0;
            for (int i = min_index; i > 0; i--)
            {
                string[] pieces = tempArray[i].Split(delimiterChars);
                x = Convert.ToSingle(pieces[0]);
                y = Convert.ToSingle(pieces[1]);
                prev_x = Convert.ToSingle(piecesprev[0]);
                prev_y = Convert.ToSingle(piecesprev[1]);
                if (i < min_index)
                {

                    x_diff = prev_x - x;
                    y_diff = (prev_y - y) * -1;
                    //MessageBox.Show("x=" + x + " y=" + y + " prev_x=" + prev_x + " prev_y=" + prev_y + " x_diff=" + x_diff + " y_diff=" + y_diff);
                    y_steps = Convert.ToInt32(Math.Round(Globals.leadtocrossfactor * leadstepspersecond) * y_diff / x_diff);
                    Array.Resize(ref commandsLHS, commandsLHS.Length + 1);
                    commandsLHS[array_i]= "LI " + x_diff + " " + leadsteps + " " + y_steps;
                    array_i++;
                }
                piecesprev = tempArray[i].Split(delimiterChars);
            }
        }

        private void buttonPenCode_Click(object sender, EventArgs e)
        {
            string[] LHS = new string[0];
            string[] RHS = new string[0];
            textArcLeft.Text = "";
            textArcRight.Text = "";            
            turnArc(textArc.Lines, "1000",  ref LHS, ref RHS);            
            for (int i = 0; i < LHS.Length; i++)
            {

                textArcLeft.Text += LHS[i] + System.Environment.NewLine;            
            }
            for (int i = 0; i < RHS.Length; i++)
            {
                textArcRight.Text += RHS[i] + System.Environment.NewLine;
                ;
            }
        }

        public void getSavedArc(string SavedName)
        {
            Globals.aKey = Globals.ArcKey.OpenSubKey(SavedName);
            textArc.Text = Convert.ToString(Globals.aKey.GetValue("Coords"));
            textArcName.Text = comboArcName.Text;
        }


        private void comboArcName_SelectedIndexChanged(object sender, EventArgs e)
        {
            getSavedArc(comboArcName.Text);

        }

        private void textCyl_TextChanged(object sender, EventArgs e)
        {

        }

        private void buttonArcSave_Click(object sender, EventArgs e)
        {
            Globals.aKey = Globals.ArcKey.CreateSubKey(textArcName.Text);
            Globals.aKey.SetValue("Coords",textArc.Text);
            comboArcName.Items.Clear();
            string[] ArcKeys = Globals.ArcKey.GetSubKeyNames();
            foreach (string thisKey in ArcKeys)
            {
                comboArcName.Items.Add(thisKey);
                comboArcName.Text = textArcName.Text;
            }
            MessageBox.Show("Parameters saved as " + textArcName.Text);
        }

        private void label59_Click(object sender, EventArgs e)
        {

        }

        private void label63_Click(object sender, EventArgs e)
        {

        }

        private void buttonPlugins_Click(object sender, EventArgs e)
        {
            Globals.ConfigKey.SetValue("Plugins", textPlugins.Text);
        }

        private void buttonPeck_Click(object sender, EventArgs e)
        {
            turnPeck(
             textPeckTotalmm.Text,
             textPeckmm.Text,
             textPeckSpeed.Text,
             textRetractmm.Text,
             textRetractSpeed.Text);
        }

        private void buttonPeckSave_Click(object sender, EventArgs e)
        {
            Globals.aKey = Globals.PeckKey.CreateSubKey(textPeck.Text);
            Globals.aKey.SetValue("PeckTotalmm", textPeckTotalmm.Text);
            Globals.aKey.SetValue("Peckmm", textPeckmm.Text);
            Globals.aKey.SetValue("PeckSpeed", textPeckSpeed.Text);
            Globals.aKey.SetValue("Retractmm", textRetractmm.Text);
            Globals.aKey.SetValue("RetractSpeed", textRetractSpeed.Text);

            comboPeck.Items.Clear();
            string[] PeckKeys = Globals.PeckKey.GetSubKeyNames();
            foreach (string thisKey in PeckKeys)
            {
                comboPeck.Items.Add(thisKey);
                comboPeck.Text = textPeck.Text;
            }
            MessageBox.Show("Parameters saved as " + textPeck.Text);
        }
        private void getSavedPeck(string SavedName)
        {
            Globals.aKey = Globals.PeckKey.OpenSubKey(SavedName);
            textPeckTotalmm.Text = Convert.ToString(Globals.aKey.GetValue("PeckTotalmm"));            
            textPeckmm.Text = Convert.ToString(Globals.aKey.GetValue("Peckmm"));            
            textPeckSpeed.Text = Convert.ToString(Globals.aKey.GetValue("PeckSpeed"));            
            textRetractmm.Text = Convert.ToString(Globals.aKey.GetValue("Retractmm"));            
            textRetractSpeed.Text = Convert.ToString(Globals.aKey.GetValue("RetractSpeed"));            
            textPeck.Text = comboPeck.Text;  
        }
        
        private void comboPeck_SelectedIndexChanged(object sender, EventArgs e)
        {
            getSavedPeck(comboPeck.Text);
        }

        private void buttonAccel_Click(object sender, EventArgs e)
        {
            Globals.ConfigKey.SetValue("Plugins", textPlugins.Text);            
            Globals.ConfigKey.SetValue("AccelCross", textCAccel.Text);
            Globals.ConfigKey.SetValue("AccelLead", textLAccel.Text);            
            MessageBox.Show("Acceleration saved");
        }

        private void labelXP_Click(object sender, EventArgs e)
        {

        }

    }
}
class Globals
{
    public const float leadfactor = 214.1732F; // steps per mm
    public const float crossfactor = 629.9213F; // steps per mm
    public const float leadtocrossfactor = 2.94117686F; // for  eg. 45 degrees
        
    public static float HomeXmm = 0.0F; // distance from centre line - MeasuredXmm + amount moved out (

    public static float XOffsetmm = 0.0F;
    public static float XRadius = 0.0F;
    public static float ZOffsetmm = 0.0F;

    public static float PartingToolWidthmm = 0.0F;
    public static float PartingToolXDistance = 0.0F;
    public static float PartingToolZDistance = 0.0F;

    public static float ToolWidth = 0.0F;

    public static float LAccel = 0.0F;
    public static float CAccel = 0.0F;

    public static RegistryKey CurrentUserKey;
    public static RegistryKey SoftwareKey;
    public static RegistryKey MyfordKey;
    public static RegistryKey ManualKey;
    public static RegistryKey CylinderKey;
    public static RegistryKey PartingKey;
    public static RegistryKey PeckKey;
    public static RegistryKey ArcKey;    
    public static RegistryKey ConfigKey;
    public static RegistryKey aKey;


}
