#import <Foundation/Foundation.h>
#import <WebKit/WebKit.h>

static WKWebView *PS_WebView = nil;
static NSString *PS_UserAgent = nil;

static BOOL PS_UserAgentRequestComplete = NO;
static BOOL PS_UserAgentRequestInProgress = NO;

extern "C"
{

#pragma mark - Privacy Preferences

    const char* GetStringPreference(const char* key)
    {
        NSString* nsKey =
            [NSString stringWithUTF8String:key];

        NSString* value =
            [[NSUserDefaults standardUserDefaults]
                stringForKey:nsKey];

        return strdup(
            value ? value.UTF8String : "");
    }

    int GetIntPreference(const char* key)
    {
        NSString* nsKey =
            [NSString stringWithUTF8String:key];

        return (int)
            [[NSUserDefaults standardUserDefaults]
                integerForKey:nsKey];
    }

    bool HasPreference(const char* key)
    {
        NSString* nsKey =
            [NSString stringWithUTF8String:key];

        return [[NSUserDefaults standardUserDefaults]
                    objectForKey:nsKey] != nil;
    }


#pragma mark - User Agent

	void PS_RequestUserAgent()
    {
        dispatch_async(dispatch_get_main_queue(), ^
        {
            // Prevent overlapping requests.
            if (PS_UserAgentRequestInProgress)
            {
                return;
            }

            PS_UserAgentRequestInProgress = YES;
            PS_UserAgentRequestComplete = NO;

            // Strong reference keeps the WebView alive until
            // JavaScript evaluation completes.
            PS_WebView =
                [[WKWebView alloc] initWithFrame:CGRectZero];

            [PS_WebView
                evaluateJavaScript:@"navigator.userAgent"
                completionHandler:^(id result, NSError *error)
                {
                    if (error == nil &&
                        result != nil &&
                        [result isKindOfClass:[NSString class]])
                    {
                        PS_UserAgent =
                            [(NSString *)result copy];
                    }
                    else
                    {
                        PS_UserAgent = nil;
                    }

                    // The WebView is no longer needed.
                    PS_WebView = nil;

                    PS_UserAgentRequestInProgress = NO;
                    PS_UserAgentRequestComplete = YES;
                }];
        });
    }

    bool PS_IsUserAgentRequestComplete()
    {
        return PS_UserAgentRequestComplete;
    }

    const char *PS_GetUserAgent()
    {
        if (PS_UserAgent == nil)
        {
            return "";
        }

        return [PS_UserAgent UTF8String];
    }

    void PS_ReleaseUserAgent()
    {
        PS_UserAgent = nil;
    }
    
    
}
