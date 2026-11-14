#import <Cocoa/Cocoa.h>
int main(int argc,char **argv){@autoreleasepool{
 if(argc!=2)return 1;
 NSBitmapImageRep *im=(id)[NSBitmapImageRep imageRepWithContentsOfFile:@(argv[1])];if(!im)return 1;
 int n=0,x1=im.pixelsWide,y1=im.pixelsHigh,x2=-1,y2=-1;
 // Ignore the macOS title bar and its yellow minimize control.
 for(int y=36;y<im.pixelsHigh;y++){@autoreleasepool{for(int x=0;x<im.pixelsWide;x++){
  NSColor *c=[[im colorAtX:x y:y] colorUsingColorSpace:NSColorSpace.sRGBColorSpace];
  if(c.redComponent<.88||c.greenComponent<.88||c.blueComponent>.53)continue;
  x1=MIN(x1,x);x2=MAX(x2,x);y1=MIN(y1,y);y2=MAX(y2,y);n++;
 }}}
 printf("%d %d %d %d %d\n",n,x1,y1,x2,y2);return n==0;
}}
