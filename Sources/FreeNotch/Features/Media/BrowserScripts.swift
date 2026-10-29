import Foundation

enum BrowserScripts {
    static let mediaDomains = [
        "youtube.com", "youtu.be", "soundcloud.com", "twitch.tv", "vimeo.com", "netflix.com",
        "open.spotify.com", "music.apple.com", "deezer.com", "primevideo.com", "disneyplus.com",
        "dailymotion.com", "bandcamp.com", "tidal.com", "kick.com", "blutv", "exxen", "puhutv",
        "gain.com.tr", "tabii.com", "mubi.com", "hbomax.com", "max.com", "x.com", "instagram.com"
    ]

    static let probe = """
    (function(){var ms=Array.prototype.slice.call(document.querySelectorAll('video,audio')).filter(function(e){return e.duration>0||e.currentTime>0});\
    var md=navigator.mediaSession&&navigator.mediaSession.metadata;\
    var art='';if(md&&md.artwork&&md.artwork.length){art=md.artwork[md.artwork.length-1].src}\
    var fv=document.visibilityState==='visible'&&document.hasFocus();\
    if(!ms.length){if(!md)return '';var st=navigator.mediaSession.playbackState;\
    return JSON.stringify({t:md.title||document.title,a:md.artist||'',al:md.album||'',d:0,c:0,p:st==='playing',art:art,u:location.href,f:fv})}\
    var m=ms.filter(function(e){return !e.paused})[0]||ms[0];\
    return JSON.stringify({t:(md&&md.title)||document.title,a:(md&&md.artist)||location.hostname.replace('www.',''),al:(md&&md.album)||'',\
    d:isFinite(m.duration)?m.duration:0,c:m.currentTime,p:!m.paused,art:art,u:location.href,f:fv})})()
    """

    static let toggle = """
    (function(){var ms=Array.prototype.slice.call(document.querySelectorAll('video,audio'));\
    var m=ms.filter(function(e){return !e.paused})[0]||ms[0];if(!m)return '';if(m.paused){m.play()}else{m.pause()}return 'ok'})()
    """

    static let next = """
    (function(){var b=document.querySelector('.ytp-next-button,[data-testid=control-button-skip-forward],.skipControl__next,button[aria-label=Next]');\
    if(b){b.click();return 'ok'}var m=document.querySelector('video,audio');if(m){m.currentTime=Math.min(m.duration||1e9,m.currentTime+10)}return 'ok'})()
    """

    static let previous = """
    (function(){var b=document.querySelector('.ytp-prev-button,[data-testid=control-button-skip-back],.skipControl__previous,button[aria-label=Previous]');\
    if(b&&b.offsetParent!==null){b.click();return 'ok'}var m=document.querySelector('video,audio');if(m){m.currentTime=Math.max(0,m.currentTime-10)}return 'ok'})()
    """

    static func seek(_ seconds: Double) -> String {
        "(function(){var ms=Array.prototype.slice.call(document.querySelectorAll('video,audio'));var m=ms.filter(function(e){return !e.paused})[0]||ms[0];if(m){m.currentTime=\(seconds)}return 'ok'})()"
    }

    static let tabTimeout = 2

    static func scan(_ app: BrowserApp, allTabs: Bool, skip: Set<String> = []) -> String {
        let js = AppleScriptRunner.escape(probe)
        let domains = mediaDomains.map { "\"\($0)\"" }.joined(separator: ", ")
        let all = allTabs ? "true" : "false"
        let skipped = skip.map { "\"\(AppleScriptRunner.escape($0))\"" }.joined(separator: ", ")

        if app == .safari {
            return """
            tell application "Safari"
                set out to {}
                set doms to {\(domains)}
                set skips to {\(skipped)}
                repeat with wi from 1 to count of windows
                    try
                        set w to window wi
                        set urls to URL of tabs of w
                        set ai to index of current tab of w
                        repeat with i from 1 to count of urls
                            set u to item i of urls as string
                            set key to ("w" & wi & "t" & i)
                            set ok to (i = ai) or \(all)
                            if not ok then
                                repeat with d in doms
                                    if u contains d then
                                        set ok to true
                                        exit repeat
                                    end if
                                end repeat
                            end if
                            if ok and (skips does not contain key) then
                                try
                                    with timeout of \(tabTimeout) seconds
                                        set r to do JavaScript "\(js)" in tab i of w
                                    end timeout
                                    if r is not missing value and r is not "" then set end of out to (key & "|" & r)
                                on error msg number n
                                    set end of out to ("!ERR|" & key & "|" & n & "|" & msg)
                                end try
                            end if
                        end repeat
                    end try
                end repeat
                return out
            end tell
            """
        }

        return """
        tell application "\(app.appName)"
            set out to {}
            set doms to {\(domains)}
            set skips to {\(skipped)}
            repeat with w in windows
                try
                    set urls to URL of tabs of w
                    set ids to id of tabs of w
                    set ai to 0
                    try
                        set ai to active tab index of w
                    end try
                    repeat with i from 1 to count of urls
                        set u to item i of urls as string
                        set key to (item i of ids as string)
                        set ok to (i = ai) or \(all)
                        if not ok then
                            repeat with d in doms
                                if u contains d then
                                    set ok to true
                                    exit repeat
                                end if
                            end repeat
                        end if
                        if ok and (skips does not contain key) then
                            try
                                with timeout of \(tabTimeout) seconds
                                    set r to execute (tab i of w) javascript "\(js)"
                                end timeout
                                if r is not missing value and r is not "" then set end of out to (key & "|" & r)
                            on error msg number n
                                set end of out to ("!ERR|" & key & "|" & n & "|" & msg)
                            end try
                        end if
                    end repeat
                end try
            end repeat
            return out
        end tell
        """
    }

    static func run(_ app: BrowserApp, tab: String, js: String) -> String {
        let code = AppleScriptRunner.escape(js)
        if app == .safari {
            let parts = tab.dropFirst().split(separator: "t")
            guard parts.count == 2 else { return "" }
            return "with timeout of 3 seconds\ntell application \"Safari\" to do JavaScript \"\(code)\" in tab \(parts[1]) of window \(parts[0])\nend timeout"
        }
        return """
        with timeout of 3 seconds
            tell application "\(app.appName)"
                repeat with w in windows
                    repeat with t in tabs of w
                        if (id of t as string) is "\(AppleScriptRunner.escape(tab))" then return execute t javascript "\(code)"
                    end repeat
                end repeat
            end tell
        end timeout
        """
    }
}
