using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TfsEd.Core.Credentials;

/// <summary>Windows Credential Manager (generic credentials, target <c>tfsed:&lt;collection&gt;</c>).</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public string Description => "Windows Credential Manager";

    public string? Get(CollectionUrl collection)
    {
        if (!CredRead(TargetName(collection), CredTypeGeneric, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            return error == ErrorNotFound ? null : throw new CredentialStoreException($"CredRead failed (Win32 error {error}).");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            return credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0
                ? null
                : Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / sizeof(char));
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public void Set(CollectionUrl collection, string secret)
    {
        var target = Marshal.StringToHGlobalUni(TargetName(collection));
        var userName = Marshal.StringToHGlobalUni("PAT");
        var blob = Marshal.StringToHGlobalUni(secret);
        try
        {
            var credential = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                UserName = userName,
                CredentialBlob = blob,
                CredentialBlobSize = secret.Length * sizeof(char),
                Persist = CredPersistLocalMachine,
            };
            if (!CredWrite(ref credential, 0))
            {
                throw new CredentialStoreException($"CredWrite failed (Win32 error {Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(blob);
            Marshal.FreeHGlobal(userName);
            Marshal.FreeHGlobal(target);
        }
    }

    public bool Remove(CollectionUrl collection)
    {
        if (CredDelete(TargetName(collection), CredTypeGeneric, 0))
        {
            return true;
        }

        var error = Marshal.GetLastWin32Error();
        return error == ErrorNotFound ? false : throw new CredentialStoreException($"CredDelete failed (Win32 error {error}).");
    }

    private static string TargetName(CollectionUrl collection) => $"{CredentialStoreFactory.ServiceName}:{collection}";

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCredential
    {
        public int Flags;
        public int Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public int LastWrittenLow;
        public int LastWrittenHigh;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
