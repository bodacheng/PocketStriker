//
//  NotificationService.m
//  notification
//
//  Created by pokepia on 2022/02/02.
//
#import "NotificationService.h"

@interface NotificationService ()

@property (nonatomic, strong) void (^contentHandler)(UNNotificationContent *contentToDeliver);
@property (nonatomic, strong) UNMutableNotificationContent *bestAttemptContent;

@end

@implementation NotificationService

- (void)didReceiveNotificationRequest:(UNNotificationRequest *)request withContentHandler:(void (^)(UNNotificationContent * _Nonnull))contentHandler {
    self.contentHandler = contentHandler;
  
    NSString* imageUrl = [[request.content.userInfo objectForKey:@"fcm_options"] objectForKey:@"image"];
  
    if(imageUrl) {
        NSURLSession *session = [NSURLSession sessionWithConfiguration:[NSURLSessionConfiguration defaultSessionConfiguration]];
        NSURLSessionTask *task = [session dataTaskWithURL:[NSURL URLWithString:imageUrl] completionHandler:^(NSData * _Nullable data, NSURLResponse * _Nullable response, NSError * _Nullable error) {
            NSURL *writePath = [[NSURL fileURLWithPath:NSTemporaryDirectory()] URLByAppendingPathComponent:@"tmp.png"];
            [data writeToURL:writePath atomically:YES];
            UNNotificationAttachment *attachment = [UNNotificationAttachment attachmentWithIdentifier:@"ImageAttachment" URL:writePath options:nil error:nil];
            UNMutableNotificationContent *content = [request.content mutableCopy];
            content.attachments = @[attachment];
            
            self.contentHandler(content);
            
        }];
        [task resume];
    } else {
        self.contentHandler(self.bestAttemptContent);
    }
    
    
}

- (void)serviceExtensionTimeWillExpire {
    self.contentHandler(self.bestAttemptContent);
}

@end
