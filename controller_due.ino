#include <AccelStepper.h>
#include <LiquidCrystal.h>
#include <stdlib.h>

/* V 2.0 - incl acceleration */

/* blue wire is gnd */

// leadscrew 

#define DIR1_PIN 9 // to direction+ on DQ542MA green  
#define STEP1_PIN 10 // to pulse+ on DQ542MA orange 

// crosslide

#define DIR2_PIN 7 // to direction+ on DQ542MA green 
#define STEP2_PIN 6 // to pulse+ on DQ542MA orange

#define enablepin 8 // to enable+ on DQ542MA white
#define waitPin 11 //q to switch 
#define joyLeft 12
#define joyRight 5
#define joyIn 3 
#define joyOut 4

#define powerpin 14

#define quad_A 2 // encoder A - this must be reserved for the encoder hardware timer
#define quad_B 13 // encoder B - this must be reserved for the encoder hardware timer 

/*
cross and lead directions - facing lathe

          +
          +
     ----- ++++++
          -
          -          

 Commands :
  Can use 
  M1, M0          Mains power on SSR on or off - controls stepper power supply and lathe power
  Lxxx;Syyy;      leadscrew xxx steps, at yyy per second  - positive is right, negative left
  Cxx;Syyy;       crosslide xxx steps, at yyy per second  - positive is in, negative out  
  Dxxx;           at string start, repeat entire string xxx times (duplicate)
  
  Bxxx;Syyy;Szzz  go leadscrew left xxx steps at yyy per second, crossslide in at zzz per second 

(tied to leadscrew) 
                  (if xxx is negative then right - if zzz is negative then out)

  K                cut motors
  Wxxx;            wait xxx milliseconds
  W0;              wait for button on waitPin to be pressed
  
  Xn.n;            Set X 
  Zn.n;            Set Z 
  Ax;Sy;           Acceleration : x1=leadscrew x2=crosslide y=factor to multiply speed by
                   B command will not use acceleration
                   S0 means do not use acceleration  
  Q                Quiet mode 0=disable 1=enable (1 means quiet, only sends response after full command string is consumed
  J                joystick 0=disable 1=enable
  #                end of command string
  
*/

AccelStepper stepper1(1,STEP1_PIN, DIR1_PIN); // leadscrew
AccelStepper stepper2(1,STEP2_PIN, DIR2_PIN); // crosslide


char commandin[2550];
char rest[2550];
char str[2550];
char comm[2550];
float commVal[2550];
char floatval[2550];

char output1[40];
char output2[40];

int x;
unsigned long d;
int charreader;
int commandstringlength=0;
unsigned long timerStart;
unsigned long timerEnd;
float commVal1;
float commVal2;
float commVal3;
float LAccel=0.0F;
float CAccel=0.0F;

char currentProcess;
int currentCommand=0;
int numCommands=0;
int numDups=0;
int currentDup=0;

boolean processing=false;
boolean allDone=true;
boolean joystick=false;
boolean quiet=true;

int globalLeadSteps=0;
int globalCrossSteps=0;

unsigned long currMillis=millis();
unsigned long prevMillis=millis();

// lcd pins are    4   6  11  12  13  14
// connected to the following pins on the Due
LiquidCrystal lcd(45, 43, 53, 51, 49, 47);

void setup() { 

pinMode(powerpin,OUTPUT);
digitalWrite(powerpin, LOW);    
  
Serial.begin(115200);  
  lcd.begin(20, 4);
//lcdprint1("12345678901234567890");
  lcdprint1("Initialising        ");

pinMode(enablepin,OUTPUT);
digitalWrite(enablepin, LOW);  
pinMode(waitPin,INPUT);
digitalWrite(waitPin, HIGH); // enable internal resistor

pinMode(joyLeft,INPUT);
digitalWrite(joyLeft, HIGH);
pinMode(joyRight,INPUT);
digitalWrite(joyRight, HIGH);
pinMode(joyIn,INPUT);
digitalWrite(joyIn, HIGH);
pinMode(joyOut,INPUT);
digitalWrite(joyOut, HIGH);

stepper1.setMaxSpeed(3000);
stepper2.setMaxSpeed(3000);
stepper1.setAcceleration(0);
stepper2.setAcceleration(0);
delay(1000);
lcd.clear();
//lcdprint1("12345678901234567890");
  lcdprint1("Ready               ");
}   

void loop() { 
if(Serial.available()>0) { 
   charreader = Serial.read();     
   switch (char(charreader)) {
      case 'K':           
//         digitalWrite(enablepin, HIGH);   
         stepper1.stop();     
         stepper2.stop();     
         sendXZImmediate();         
         Serial.write("PC #"); // command sequence completed          
       //lcdprint1("12345678901234567890");
         lcdprint1("Killed              ");
         commandstringlength=0;   
         currentCommand=0;  
         numDups=0;
         currentDup=0;         
         allDone=true;
         processing=false;    
         break;
      case '#':
         commandin[commandstringlength] = charreader;          
         parse();           
         commandstringlength=0;                      
//         digitalWrite(enablepin, LOW);           
         break;
      default :
         commandin[commandstringlength] = charreader; 
         commandstringlength++;
         break;           
     }  // switch
  } // Serial.available

if (!processing) {   
  if (joystick==true) {
  if (digitalRead(joyLeft)==LOW) {
     stepper1.move(-99999999L);       
     if (digitalRead(waitPin)==LOW) {
        stepper1.setSpeed(-1500); 
        stepper1.setAcceleration(-1500*LAccel);           
        }
      else {
        stepper1.setSpeed(-120);      
        stepper1.setAcceleration(-120*LAccel);        
      }    
     runMotor(1); 
   }
   
  else if (digitalRead(joyRight)==LOW) {
     stepper1.move(99999999L);       
     if (digitalRead(waitPin)==LOW) {
        stepper1.setSpeed(1500); 
        stepper1.setAcceleration(1500*LAccel);        
        }
     else {
        stepper1.setSpeed(120);  
        stepper1.setAcceleration(120*LAccel);        
      }    
     runMotor(1);
   }   
  else if (digitalRead(joyIn)==LOW) {
     stepper2.move(99999999L);       
     if (digitalRead(waitPin)==LOW) {
        stepper2.setSpeed(2000); 
        stepper2.setAcceleration(2000*CAccel);        
        }
     else {
        stepper2.setSpeed(100);
        stepper2.setAcceleration(100*CAccel);                
      }    
     runMotor(2);
          
   }   
  else if (digitalRead(joyOut)==LOW) {
     stepper2.move(-99999999L);       
     if (digitalRead(waitPin)==LOW) {     
       stepper2.setSpeed(-2000); 
        stepper2.setAcceleration(-2000*CAccel);               
        }
     else {
        stepper2.setSpeed(-100);      
        stepper2.setAcceleration(-100*CAccel);                       
      }    
     runMotor(2);     
   }  
   else {
   //digitalWrite(13, HIGH);
   sendXZJoystick();    
   } 
   } // joystick enabled
//   else
 //  digitalWrite(13, LOW);
     
   if (allDone) {
     return;
   }  // allDone   

   
   processing=true;
   currentProcess=comm[currentCommand];      
   //Serial.println(currentProcess);
   
   if (currentProcess=='W') {
       commVal1=commVal[currentCommand];
       if (commVal1>0) {
          if (quiet==0) {
          //lcdprint1("12345678901234567890");
            lcdprint1("Wait time           ");
            Serial.write("WT ");
            Serial.print(commVal1,0);
            Serial.write(" #");
            }
          timerStart=millis();
          }
       else {
          if (quiet==0) {       
           //lcdprint1("12345678901234567890");
             lcdprint1("Wait button         ");
             Serial.write("WB ");
             Serial.write("#");
          }
       }   
       return;      
       } // W

   if (currentProcess=='Q') {
      commVal1=commVal[currentCommand];
      respond_to_pc("Q #");
      return;
   } // send position   

   if (currentProcess=='M') {
      commVal1=commVal[currentCommand];
      globalLeadSteps=commVal1;
      if (commVal1==0) 
        Serial.write("M0 #");   
      else
        Serial.write("M1 #");   
      return;   
   } // set mains
      
   if (currentProcess=='Z') {
      commVal1=commVal[currentCommand];
      globalLeadSteps=commVal1;
      respond_to_pc("Z #");   
      return;   
   } // set Z

   if (currentProcess=='X') {
      commVal1=commVal[currentCommand];     
      globalCrossSteps=commVal1;
      respond_to_pc("X #");   
      return;         
   } // set X
   
   if (currentProcess=='J') {
      commVal1=commVal[currentCommand];
      if (commVal1==1) {
	 joystick=true;
      }
      else {     
         joystick=false;
      }         
      Serial.write("J ");
      Serial.write("#");
      Serial.flush();         
      return;   
   } // set joystick on/off

   if (currentProcess=='A') { 
       if (commVal[currentCommand]==1) {
          currentCommand++;                
          LAccel=commVal[currentCommand];      
       }
       if (commVal[currentCommand]==2) {
          currentCommand++;                
          CAccel=commVal[currentCommand];      
       }
      respond_to_pc("A #");   
      }
      
   if (currentProcess=='L') { 
       commVal1=commVal[currentCommand];
       currentCommand++;                
       commVal2=commVal[currentCommand];            
       stepper1.setCurrentPosition(0);
       stepper1.move(commVal1);  
       stepper1.setSpeed(commVal2);
       stepper1.setAcceleration(commVal2*LAccel);                             
       if (commVal1 >0.0) {
          //lcdprint1("12345678901234567890");
            lcdprint1("Right               ");
          if (quiet==false) {
              sprintf(output1,"LR %0.0f %0.0f #",commVal1,commVal2);
              Serial.print(output1);
             }
          } 
       else {
            //lcdprint1("12345678901234567890");
              lcdprint1("Left                ");
	      if (quiet==false) {
              sprintf(output1,"LL %0.0f %0.0f #",commVal1*-1.0,commVal2*-1.0);
              Serial.print(output1);
			  }
          }        
       } // L

   if (currentProcess=='C') {
       commVal1=commVal[currentCommand];
       currentCommand++;                
       commVal2=commVal[currentCommand];            
       stepper2.setCurrentPosition(0);
       stepper2.move(commVal1);  
       stepper2.setSpeed(commVal2);              
       stepper2.setAcceleration(commVal2*CAccel);                             
       if (commVal1 >0.0) {
        //lcdprint1("12345678901234567890");
          lcdprint1("In                  ");
          if (quiet==false) {
              sprintf(output1,"CI %0.0f %0.0f #",commVal1,commVal2);
              Serial.print(output1);
	      }
         } 
       else {
         //lcdprint1("12345678901234567890");
           lcdprint1("Out                 ");
	   if (quiet==false) {
              sprintf(output1,"CO %0.0f %0.0f #",commVal1*-1.0,commVal2*-1.0);
              Serial.print(output1);
	      }
         } 
       } // C

   if (currentProcess=='B') { // both, leadscrew controls the stop
       commVal1=commVal[currentCommand];
       currentCommand++;                
       commVal2=commVal[currentCommand];            
       currentCommand++;                
       commVal3=commVal[currentCommand];                   
       stepper1.setCurrentPosition(0);       
       stepper2.setCurrentPosition(0);
       stepper1.move(commVal1);  
       stepper1.setSpeed(commVal2);              
       stepper1.setAcceleration(0);                                    
       if (commVal3>0) {
          stepper2.move(99999999L);  
          }
       else {
          stepper2.move(-99999999L);                  
          }           
       stepper2.setSpeed(commVal3);                   
       stepper2.setAcceleration(0);                                           
       if (commVal2 >0.0 && commVal3 >0.0) {
          //lcdprint1("12345678901234567890");
            lcdprint1("In right            ");
	   if (quiet==false) {
              sprintf(output1,"BRI %0.0f %0.0f %0.0f#",commVal1,commVal2,commVal3);
              Serial.print(output1);
              lcdprint2(output1);
	      }
         } // BRI
         
       if (commVal2 >=0.0 && commVal3 <0.0) {
          //lcdprint1("12345678901234567890");
            lcdprint1("Out right           ");
	   if (quiet==false) {
              sprintf(output1,"BRO %0.0f %0.0f %0.0f#",commVal1,commVal2,commVal3*-1.0);
              Serial.print(output1);
              lcdprint2(output1);              
	      }         
          } // BRO
         
       if (commVal2 <0.0 && commVal3 >=0.0) {
          //lcdprint1("12345678901234567890");
            lcdprint1("In left             ");
	   if (quiet==false) {
              sprintf(output1,"BLI %0.0f %0.0f %0.0f#",commVal1*-1.0,commVal2*-1.0,commVal3);
              Serial.print(output1);
              lcdprint2(output1);              
	      }         
          } // BLI
         
       if (commVal2 <0.0 && commVal3 <0.0) {
          //lcdprint1("12345678901234567890");
            lcdprint1("Out left            ");
	   if (quiet==false) {
              sprintf(output1,"BLO %0.0f %0.0f %0.0f#",commVal1*-1.0,commVal2*-1.0,commVal3*-1.0);
              Serial.print(output1);
              lcdprint2(output1);              
	      }                  
          }  // BLO      
      } // B    
   
   }  // !processing
  
if (processing) {   
     if (currentProcess=='Q')  {    
       if (commVal1==1) {
         quiet=true; }
       else {
         quiet=false; 
       }
       respond_to_pc("CC #");      
       incrementCommands(); 
       return;        
   }
   
   if (currentProcess=='M')  {    
       if (commVal1==1) {
          digitalWrite(powerpin,HIGH); 
          }
       else {
          digitalWrite(powerpin,LOW);
          }
       respond_to_pc("CC #");      
       incrementCommands(); 
       return;        
   }   
   if (currentProcess=='X')  {    
       respond_to_pc("CC #");          
        incrementCommands(); 
        return;        
   }
   if (currentProcess=='Z')  {    
       respond_to_pc("CC #");          
        incrementCommands();                      
        return;        
   }
   if (currentProcess=='J')  {    
       respond_to_pc("CC #");          
       incrementCommands();                      
       return;        
   }   

   if (currentProcess=='A')  {    
       respond_to_pc("CC #");          
       incrementCommands();                      
       return;        
   }   

   
   if (currentProcess=='W')  {
      if (commVal1==0) { 
         if (digitalRead(waitPin)==LOW) {
            respond_to_pc("CC #");          
            incrementCommands();                  
         } // LOW
         return;
       } // Val1=0
      timerEnd=millis(); 
      if ((timerEnd-timerStart)>=commVal1) {       
         respond_to_pc("CC #");          
         incrementCommands();
          return;         
         } // done 
      } // W       
      
   if (currentProcess=='L') {
       if (commVal1==0 || (stepper1.distanceToGo()<=0 && commVal1>0) || (stepper1.distanceToGo()>=0 && commVal1<0)) {
         respond_to_pc ("CC #");                       
         incrementCommands();
          return;
          } // done
       runMotor(1);
      }  // L
      
   if (currentProcess=='C') {
       if (commVal1==0 || (stepper2.distanceToGo()<=0 && commVal1>0) || (stepper2.distanceToGo()>=0 && commVal1<0)) {
          respond_to_pc ("CC #");
          incrementCommands();
          return;
          } // done                 
       runMotor(2);
      }  // C

   if (currentProcess=='B') {
       if (commVal1==0 || (stepper1.distanceToGo()<=0 && commVal1>0) || (stepper1.distanceToGo()>=0 && commVal1<0)) {
          respond_to_pc("CC #");          
          incrementCommands();
          return;
          } // done
	   runMotor(1);
	   runMotor(2);
       } // B
       
   } // processing      
} // loop

void parse() {
numCommands=0;
currentDup=1;
allDone=false;
processing=false;
while (char(commandin[0])!='#') {
   sscanf(commandin,"%[^';']%[^\n]",str,rest);
     x=sscanf(str,"%c%s",&comm[numCommands],floatval);
  commVal[numCommands]=atof(floatval);   
   numCommands++;
   strcpy(commandin,rest+1);
   }    
if (comm[0]=='D') {
   currentCommand=1;   
   numDups=commVal[0];   
   if (numDups==0) {
      numDups=1;
      }
   Serial.write("D 1 #");         
   }  
else {
   currentCommand=0; 
   numDups=1;
   }   
   //Serial.println(numDups);      
   //Serial.println(numCommands);         
} // parse

void incrementCommands() {
processing=false;  
currentProcess=' ';
if (currentCommand<numCommands) {
   currentCommand++;  
   }
if (currentCommand==numCommands) {
    if (currentDup==numDups) {
       allDone=true;
       //lcdprint1("12345678901234567890");
         lcdprint1("Ready               ");
         lcdprint2(" ");

         Serial.write("PC "); // command completed                 
         Serial.print(globalCrossSteps);
         Serial.write(" ");          
         Serial.print(globalLeadSteps);         
         Serial.write(" #");          
         return;
         }
    currentDup++;
    if (quiet==false) {
       Serial.write("D ");
       Serial.print(currentDup);    
       Serial.write(" #"); 
       }
    if (comm[0]=='D') {
       currentCommand=1;   
       }  
    else {
       currentCommand=0; 
       }
   }       
}

void lcdprint1(char * msg) {
lcd.setCursor(0,0);
lcd.print(msg);
}

void lcdprint2(char * msg) {
lcd.setCursor(0,1);
lcd.print("                    ");
lcd.setCursor(0,1);
lcd.print(msg);
}


void runMotor(int motor) {
if (motor==1) {
   if (stepper1.runSpeed()) {       
       if (stepper1.speed()>=0) {
          globalLeadSteps++;
		  }
       else {
          globalLeadSteps--;
		  }  	   
      }
   }
if (motor==2) {
   if (stepper2.runSpeed()) {
       if (stepper2.speed()>=0) {
          globalCrossSteps++;
		  }
       else {
          globalCrossSteps--;
		  }  	  
   
      }
   }   
}

void respond_to_pc (char * msg) {
  if (quiet==false) {
   Serial.write(msg);   
  }
}

void sendXZJoystick() {
   currMillis=millis();
   if ((currMillis-prevMillis)>100) {
       prevMillis=currMillis;       
       Serial.write("P ");
       Serial.print(globalCrossSteps);
       Serial.write(" ");          
       Serial.print(globalLeadSteps);
       Serial.write(" #");      
       } 
}
	   
void sendXZImmediate() {
       Serial.write("P ");
       Serial.print(globalCrossSteps);
       Serial.write(" ");          
       Serial.print(globalLeadSteps);
       Serial.write(" #");      
       } 