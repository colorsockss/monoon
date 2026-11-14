#import <Carbon/Carbon.h>
#import <Foundation/Foundation.h>
int main(int count,char** v){@autoreleasepool{
TISInputSourceRef s=TISCopyCurrentKeyboardInputSource();
if(!s)return 1;
NSString *cur=(__bridge NSString*)TISGetInputSourceProperty(s,kTISPropertyInputSourceID);
if(count==1){puts(cur.UTF8String);return 0;}
NSArray *all=CFBridgingRelease(TISCreateInputSourceList(NULL,true));
for (id obj in all){TISInputSourceRef item=(__bridge TISInputSourceRef)obj;
NSString *name=(__bridge NSString*)TISGetInputSourceProperty(item,kTISPropertyInputSourceID);
if([name isEqualToString:@(v[1])]){
if(count==2){puts(TISGetInputSourceProperty(item,kTISPropertyInputSourceIsEnabled)==kCFBooleanTrue?"enabled":"disabled");return 0;}
if(!strcmp(v[2],"select"))return TISSelectInputSource(item)!=noErr;
}
if(!strcmp(v[1],"korean")&&[name containsString:@"Korean"])puts(name.UTF8String);
}return 2;}}
