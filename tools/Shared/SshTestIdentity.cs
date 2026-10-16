// The disposable NSS lab uses this alias, independently of the developer's OS login.
internal static class SshTestIdentity
{
    internal static string Username=>Environment.GetEnvironmentVariable("SERVERDESK_TEST_SSH_USER")??"serverdesk_fixture";
}
