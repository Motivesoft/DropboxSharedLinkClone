using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Sharing;
using DropBoxSharedLineClone.Properties;

namespace DropBoxSharedLineClone
{
    partial class Program
    {
        // This loopback host is for demo purpose. If this port is not
        // available on your machine you need to update this URL with an unused port.
        private const string LoopbackHost = "http://127.0.0.1:52475/";

        // URL to receive OAuth 2 redirect from Dropbox server.
        // You also need to register this redirect URL on https://www.dropbox.com/developers/apps.
        private readonly Uri RedirectUri = new Uri( LoopbackHost + "authorize" );

        // URL to receive access token from JS.
        private readonly Uri JSRedirectUri = new Uri( LoopbackHost + "token" );

        private bool Verbose;

        private bool PromptToExit;

        [DllImport( "kernel32.dll", ExactSpelling = true )]
        private static extern IntPtr GetConsoleWindow();

        [DllImport( "user32.dll" )]
        [return: MarshalAs( UnmanagedType.Bool )]
        private static extern bool SetForegroundWindow( IntPtr hWnd );

        [STAThread]
        static int Main( string[] args )
        {
            var instance = new Program();
            try
            {
                bool verboseMode = false;
                bool promptToExit = true;
                bool process = true;
                foreach ( var arg in args )
                {
                    // Look for - or / or --
                    if ( arg.StartsWith( "/" ) || arg.StartsWith( "-" ) )
                    {
                        var argument = arg.Substring(1).ToLowerInvariant();
                        if ( argument.Equals( "v" ) || argument.Equals( "-verbose" ) )
                        {
                            Console.WriteLine( "Verbose mode" );
                            verboseMode = true;
                        }
                        else if ( argument.Equals( "np" ) || argument.Equals( "-no-prompt" ) )
                        {
                            if ( verboseMode )
                            {
                                Console.WriteLine( "No prompt on exit" );
                            }
                            promptToExit = false;
                        }
                        else if ( argument.Equals( "ra" ) || argument.Equals( "-reset-all" ) )
                        {
                            if ( verboseMode )
                            {
                                Console.WriteLine( "Reset all settings" );
                            }
                            Settings.Default.Reset();
                        }
                        else if ( argument.Equals( "rsl" ) || argument.Equals( "-reset-shared-links" ) )
                        {
                            if ( verboseMode )
                            {
                                Console.WriteLine( "Reset all shared links" );
                            }
                            Settings.Default.SharedLinks.Clear();
                        }
                        else if ( argument.Equals( "?" ) || argument.Equals( "h" ) || argument.Equals( "-help" ) )
                        {
                            ShowHelp();
                            process = false;
                        }
                        else
                        {
                            Console.WriteLine( $"Unknown command line argument: {arg}" );
                            ShowHelp();
                            process = false;
                        }
                    }
                }

                if ( process )
                {
                    if ( verboseMode )
                    {
                        Console.WriteLine( "Starting..." );
                    }
                    instance.Verbose = verboseMode;
                    instance.PromptToExit = promptToExit;
                    var task = Task.Run( (Func<Task<int>>) instance.Run );

                    task.Wait();

                    return task.Result;
                }

                return 1;
            }
            catch ( Exception e )
            {
                Console.WriteLine( e );
                throw e;
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine();
            Console.WriteLine( "Clone the files from a Dropbox shared link folder to a local folder" );
            Console.WriteLine();
            Console.WriteLine( "Launch with no arguments to clone shared link folders. You will be prompted for any required configuration" );
            Console.WriteLine();
            Console.WriteLine( "Or launch with these arguments:");
            Console.WriteLine( "  -v, --verbose                  display verbose information" );
            Console.WriteLine( "  -np, --no-prompt               don't prompt the user to press a key on exit" );
            Console.WriteLine( "  -ra, --reset-all               reset all configuration");
            Console.WriteLine( "  -rsl, --reset-shared-links     reset shared link folder configuration only" );
            Console.WriteLine();
            Console.WriteLine( "After any sort of configuration reset, you will then be prompted to re-enter the details" );
        }

        private async Task<int> Run()
        {
            DropboxCertHelper.InitializeCertPinning();

            string[] scopeList = new string[] { "files.metadata.read", "files.content.read", "account_info.read", "sharing.read" };
            var uid = await AcquireAccessToken( scopeList, IncludeGrantedScopes.None );
            if ( string.IsNullOrEmpty( uid ) )
            {
                return 1;
            }

            // Specify socket level timeout which decides maximum waiting time when no bytes are received by the socket.
            var httpClient = new HttpClient( new WebRequestHandler { ReadWriteTimeout = 10 * 1000 } )
            {
                // Specify request level timeout which decides maximum time that can be spent on download/upload files.
                Timeout = TimeSpan.FromMinutes( 20 )
            };

            try
            {
                var config = new DropboxClientConfig( "SimpleOAuthApp" )
                {
                    HttpClient = httpClient
                };

                var client = new DropboxClient( Settings.Default.AccessToken, Settings.Default.RefreshToken, Settings.Default.ApiKey, Settings.Default.ApiSecret, config );
                var scopes = new string[] { "files.metadata.read", "files.content.read", "sharing.read" };
                await client.RefreshAccessToken( scopes );

                if ( Settings.Default.SharedLinks == null || Settings.Default.SharedLinks.Count == 0 )
                {
                    Settings.Default.SharedLinks = new System.Collections.Specialized.StringCollection();

                    Console.Write( "Shared link URL: " );
                    var line = Console.ReadLine();
                    while( line.Length > 0 )
                    {
                        Settings.Default.SharedLinks.Add( line );

                        Console.Write( "Additional shared link URL (leave blank to finish): " );
                        line = Console.ReadLine();
                    }
                    Settings.Default.Save();
                }

                var sharedLinks = Settings.Default.SharedLinks;
                foreach ( var sharedLinkUrl in sharedLinks )
                {
                    GetSharedLinkMetadataArg arg = new GetSharedLinkMetadataArg( sharedLinkUrl );
                    var sharedLinkMetaData = await client.Sharing.GetSharedLinkMetadataAsync( arg );

                    var localDir = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), sharedLinkMetaData.Name );
                    Directory.CreateDirectory( localDir );

                    Console.WriteLine( $"Processing shared link name: {sharedLinkMetaData.Name}" );
                    if ( Verbose )
                    {
                        Console.WriteLine( $"Shared link local folder: {localDir}" );
                    }

                    SharedLink sharedLink = new SharedLink( sharedLinkUrl );
                    ListFolderArg listFolderArg = new ListFolderArg( path: "", sharedLink: sharedLink );
                    var listFiles = await client.Files.ListFolderAsync( listFolderArg );
                    foreach ( var listFile in listFiles.Entries )
                    {
                        try
                        {
                            if ( Verbose )
                            {
                                Console.WriteLine( $"Processing: {listFile.Name}" );
                            }

                            // Get the remote object details
                            var remoteFile = listFile.AsFile;

                            // Construct a reference to the local equivalent
                            var localFile = Path.Combine( localDir, listFile.Name );
                            if ( Verbose )
                            {
                                Console.WriteLine( $"  Local filename: {localFile}" );
                            }

                            // Do we have a file already
                            if ( File.Exists( localFile ) )
                            {
                                if ( Verbose )
                                {
                                    Console.WriteLine( $"  Local file exists. Comparing timestamp" );
                                }

                                var localTimestamp = File.GetLastWriteTimeUtc( localFile );

                                if ( Verbose )
                                {
                                    Console.WriteLine( $"  Checking {remoteFile.ServerModified} with {localTimestamp}" );
                                }

                                if ( DateTime.Compare( remoteFile.ServerModified, localTimestamp ) == 0 )
                                {
                                    if ( Verbose )
                                    {
                                        Console.WriteLine( $"  Skipping unchanged file: {listFile.Name}" );
                                    }
                                    continue;
                                }
                            }

                            GetSharedLinkMetadataArg downloadArg = new GetSharedLinkMetadataArg( sharedLinkUrl, $"/{listFile.Name}" );
                            if ( Verbose )
                            {
                                Console.WriteLine( $"SharedLinkUrl: {sharedLinkUrl}" );
                                Console.WriteLine( $"    File Name: {listFile.Name}" );
                            }
                            var download = await client.Sharing.GetSharedLinkFileAsync( downloadArg );

                            Console.WriteLine( $"  Downloading: {remoteFile.Name}" );
                            using ( var ms = new MemoryStream() )
                            {
                                var bytes = await download.GetContentAsByteArrayAsync();
                                if ( bytes.Length > 0 )
                                {
                                    File.WriteAllBytes( localFile, bytes.ToArray() );
                                    File.SetCreationTimeUtc( localFile, remoteFile.ServerModified );
                                    File.SetLastWriteTimeUtc( localFile, remoteFile.ServerModified );
                                }
                                else
                                {
                                    Console.Error.WriteLine( $"No bytes downloaded" );
                                }
                            }
                        }
                        catch ( Exception ex )
                        {
                            Console.Error.WriteLine( $"Failed during download: ${ex.Message}" );
                        }
                    }

                    if ( Verbose )
                    {
                        Console.WriteLine( "Download complete!" );
                    }
                }

                if ( Verbose )
                {
                    Console.WriteLine( "All downloads complete!" );
                }

                if ( PromptToExit )
                {
                    Console.WriteLine( "Exit with any key" );
                    Console.ReadKey();
                }
            }
            catch ( HttpException e )
            {
                Console.WriteLine( "Exception reported from RPC layer" );
                Console.WriteLine( "    Status code: {0}", e.StatusCode );
                Console.WriteLine( "    Message    : {0}", e.Message );
                if ( e.RequestUri != null )
                {
                    Console.WriteLine( "    Request uri: {0}", e.RequestUri );
                }
            }

            return 0;
        }

        /// <summary>
        /// Handles the redirect from Dropbox server. Because we are using token flow, the local
        /// http server cannot directly receive the URL fragment. We need to return a HTML page with
        /// inline JS which can send URL fragment to local server as URL parameter.
        /// </summary>
        /// <param name="http">The http listener.</param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task HandleOAuth2Redirect( HttpListener http )
        {
            var context = await http.GetContextAsync();

            // We only care about request to RedirectUri endpoint.
            while ( context.Request.Url.AbsolutePath != RedirectUri.AbsolutePath )
            {
                context = await http.GetContextAsync();
            }

            context.Response.ContentType = "text/html";

            // Respond with a page which runs JS and sends URL fragment as query string
            // to TokenRedirectUri.
            using ( var file = File.OpenRead( "index.html" ) )
            {
                file.CopyTo( context.Response.OutputStream );
            }

            context.Response.OutputStream.Close();
        }

        /// <summary>
        /// Handle the redirect from JS and process raw redirect URI with fragment to
        /// complete the authorization flow.
        /// </summary>
        /// <param name="http">The http listener.</param>
        /// <returns>The <see cref="OAuth2Response"/></returns>
        private async Task<Uri> HandleJSRedirect( HttpListener http )
        {
            var context = await http.GetContextAsync();

            // We only care about request to TokenRedirectUri endpoint.
            while ( context.Request.Url.AbsolutePath != JSRedirectUri.AbsolutePath )
            {
                context = await http.GetContextAsync();
            }

            var redirectUri = new Uri( context.Request.QueryString[ "url_with_fragment" ] );

            return redirectUri;
        }

        /// <summary>
        /// Acquires a dropbox access token and saves it to the default settings for the app.
        /// <para>
        /// This fetches the access token from the applications settings, if it is not found there
        /// (or if the user chooses to reset the settings) then the UI in <see cref="LoginForm"/> is
        /// displayed to authorize the user.
        /// </para>
        /// </summary>
        /// <returns>A valid uid if a token was acquired or null.</returns>
        private async Task<string> AcquireAccessToken( string[] scopeList, IncludeGrantedScopes includeGrantedScopes )
        {
            /*
             * Not neccessary in normal processing, but a useful code block to have
             * 
                Console.Write( "Reset settings (Y/N) " );
                if ( Console.ReadKey().Key == ConsoleKey.Y )
                {
                    Settings.Default.Reset();
                }
                Console.WriteLine();
             */

            if ( string.IsNullOrEmpty( Settings.Default.ApiSecret ) )
            {
                Console.Write( "API Key: " );
                Settings.Default.ApiKey = Console.ReadLine();

                Console.Write( "API Secret Key: " );
                Settings.Default.ApiSecret = Console.ReadLine();

                Settings.Default.Save();
            }

            var accessToken = Settings.Default.AccessToken;
            var refreshToken = Settings.Default.RefreshToken;

            if ( string.IsNullOrEmpty( accessToken ) )
            {
                try
                {
                    Console.WriteLine( "Waiting for credentials." );
                    var state = Guid.NewGuid().ToString( "N" );
                    var authorizeUri = DropboxOAuth2Helper.GetAuthorizeUri( OAuthResponseType.Code, Settings.Default.ApiKey, RedirectUri, state: state, tokenAccessType: TokenAccessType.Offline, scopeList: scopeList, includeGrantedScopes: includeGrantedScopes );
                    var http = new HttpListener();
                    http.Prefixes.Add( LoopbackHost );

                    http.Start();

                    System.Diagnostics.Process.Start( authorizeUri.ToString() );

                    // Handle OAuth redirect and send URL fragment to local server using JS.
                    await HandleOAuth2Redirect( http );

                    // Handle redirect from JS and process OAuth response.
                    var redirectUri = await HandleJSRedirect( http );

                    Console.WriteLine( "Exchanging code for token" );
                    var tokenResult = await DropboxOAuth2Helper.ProcessCodeFlowAsync( redirectUri, Settings.Default.ApiKey, Settings.Default.ApiSecret, RedirectUri.ToString(), state );
                    Console.WriteLine( "Finished Exchanging Code for Token" );

                    // Bring console window to the front.
                    SetForegroundWindow( GetConsoleWindow() );
                    accessToken = tokenResult.AccessToken;
                    refreshToken = tokenResult.RefreshToken;
                    var uid = tokenResult.Uid;

                    Console.WriteLine( "Uid: {0}", uid );
                    Console.WriteLine( "AccessToken: {0}", accessToken );
                    if ( tokenResult.RefreshToken != null )
                    {
                        Console.WriteLine( "RefreshToken: {0}", refreshToken );
                        Settings.Default.RefreshToken = refreshToken;
                    }
                    if ( tokenResult.ExpiresAt != null )
                    {
                        Console.WriteLine( "ExpiresAt: {0}", tokenResult.ExpiresAt );
                    }
                    if ( tokenResult.ScopeList != null )
                    {
                        Console.WriteLine( "Scopes: {0}", String.Join( " ", tokenResult.ScopeList ) );
                    }
                    Settings.Default.AccessToken = accessToken;
                    Settings.Default.Uid = uid;
                    Settings.Default.Save();

                    http.Stop();
                    return uid;
                }
                catch ( Exception e )
                {
                    Console.WriteLine( "Error: {0}", e.Message );
                    return null;
                }
            }
            else
            {
                var uid = Settings.Default.Uid;
                if ( !string.IsNullOrEmpty( uid ) )
                {
                    return uid;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets information about the currently authorized account.
        /// <para>
        /// This demonstrates calling a simple rpc style api from the Users namespace.
        /// </para>
        /// </summary>
        /// <param name="client">The Dropbox client.</param>
        /// <returns>An asynchronous task.</returns>
        private async Task GetCurrentAccount( DropboxClient client )
        {
            try
            {
                Console.WriteLine( "Current Account:" );
                var full = await client.Users.GetCurrentAccountAsync();

                Console.WriteLine( "Account id    : {0}", full.AccountId );
                Console.WriteLine( "Country       : {0}", full.Country );
                Console.WriteLine( "Email         : {0}", full.Email );
                Console.WriteLine( "Is paired     : {0}", full.IsPaired ? "Yes" : "No" );
                Console.WriteLine( "Locale        : {0}", full.Locale );
                Console.WriteLine( "Name" );
                Console.WriteLine( "  Display  : {0}", full.Name.DisplayName );
                Console.WriteLine( "  Familiar : {0}", full.Name.FamiliarName );
                Console.WriteLine( "  Given    : {0}", full.Name.GivenName );
                Console.WriteLine( "  Surname  : {0}", full.Name.Surname );
                Console.WriteLine( "Referral link : {0}", full.ReferralLink );

                if ( full.Team != null )
                {
                    Console.WriteLine( "Team" );
                    Console.WriteLine( "  Id   : {0}", full.Team.Id );
                    Console.WriteLine( "  Name : {0}", full.Team.Name );
                }
                else
                {
                    Console.WriteLine( "Team - None" );
                }
            }
            catch ( Exception e )
            {
                throw e;
            }

        }
    }
}
