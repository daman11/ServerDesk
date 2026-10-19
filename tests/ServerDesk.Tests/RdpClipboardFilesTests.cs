using System.Buffers.Binary;
using System.Text;
using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;

public sealed class RdpClipboardFilesTests
{
    internal static byte[] Descriptors(params (string Name,bool Directory,ulong Size)[] files)
    {
        var bytes=new byte[4+files.Length*592];BinaryPrimitives.WriteUInt32LittleEndian(bytes,(uint)files.Length);
        for(var i=0;i<files.Length;i++)
        {
            var offset=4+i*592;var file=files[i];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset),file.Directory?4u:0x44u);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset+36),file.Directory?16u:128u);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset+64),(uint)(file.Size>>32));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset+68),(uint)file.Size);
            Encoding.Unicode.GetBytes(file.Name).CopyTo(bytes,offset+72);
        }
        return bytes;
    }
    [Fact] public void ParsesUnicodeFoldersNestedFilesAndEmptyFilesWithoutFilesystemAccess()
    {
        var files=RdpClipboardFiles.Parse(Descriptors(("folder żółć",true,0),("folder żółć\\日本語.txt",false,74),("empty",false,0)));
        Assert.Equal(3,files.Entries.Count);Assert.Equal(74,files.Bytes);Assert.True(files.Entries[0].IsDirectory);Assert.Equal("folder żółć\\日本語.txt",files.Entries[1].Name);
    }
    [Theory]
    [InlineData("")][InlineData(".")][InlineData("..")][InlineData("../a")][InlineData("..\\a")][InlineData("a\\..\\b")]
    [InlineData("C:\\a")][InlineData("C:a")][InlineData("\\\\server\\share")][InlineData("\\root")][InlineData("/root")]
    [InlineData("file:stream")][InlineData("a/b")][InlineData("a\\\\b")][InlineData("a.")][InlineData("a ")][InlineData("a\\.")]
    [InlineData("a\nb")][InlineData("a*b")][InlineData("a?b")][InlineData("a|b")][InlineData("a\"b")][InlineData("a<b")]
    [InlineData("CON")][InlineData("nul.txt")][InlineData("PRN")][InlineData("aux.dat")][InlineData("COM1.txt")][InlineData("lpt9")]
    [InlineData("LPT¹.txt")][InlineData("COM²")][InlineData("CONIN$")][InlineData("CONOUT$")][InlineData("folder\\NUL")]
    public void RejectsUnsafeWindowsNamesOnEveryPlatform(string name)=>Assert.Throws<InvalidDataException>(()=>RdpClipboardFiles.Parse(Descriptors((name,false,0))));
    [Fact] public void RejectsDuplicateAndFileParentCollisionsInEitherOrder()
    {
        foreach(var entries in new[] {Descriptors(("a",false,1),("A",false,1)),Descriptors(("a",false,1),("a\\b",false,1)),Descriptors(("a\\b",false,1),("a",false,1))})
            Assert.Throws<InvalidDataException>(()=>RdpClipboardFiles.Parse(entries));
    }
    [Theory][InlineData(0)][InlineData(3)][InlineData(595)][InlineData(597)]
    public void RejectsTruncatedAndTrailingDescriptors(int size)=>Assert.Throws<InvalidDataException>(()=>RdpClipboardFiles.Parse(new byte[size]));
    [Fact] public void RequiresAttributesAndKnownFileSizesAndRejectsReparsePoints()
    {
        foreach(var (flags,attributes) in new[]{(0x40u,128u),(4u,128u),(0x44u,0x480u)})
        {
            var bytes=Descriptors(("file",false,1));BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4),flags);BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40),attributes);
            Assert.Throws<InvalidDataException>(()=>RdpClipboardFiles.Parse(bytes));
        }
    }
    [Fact] public void BoundsTotalBytesBeforeAnyRemoteContentIsRequested()
    {
        Assert.Equal(RdpFileSelection.MaxBytes,RdpClipboardFiles.Parse(Descriptors(("a",false,(ulong)RdpFileSelection.MaxBytes))).Bytes);
        foreach(var bytes in new[]{Descriptors(("a",false,ulong.MaxValue)),Descriptors(("a",false,1UL<<31)),Descriptors(("a",false,(ulong)RdpFileSelection.MaxBytes),("b",false,1))})
            Assert.Throws<InvalidDataException>(()=>RdpClipboardFiles.Parse(bytes));
    }
    [Fact] public void BoundsEntriesRootsDepthAndUnicodeTermination()
    {
        Assert.Throws<InvalidDataException>(()=>RdpClipboardFiles.Parse(Descriptors(Enumerable.Range(0,257).Select(i=>($"f{i}",false,0UL)).ToArray())));
        Assert.Throws<InvalidDataException>(()=>RdpClipboardFiles.Parse(Descriptors((string.Join('\\',Enumerable.Repeat("a",65)),false,0))));
        var unterminated=Descriptors(("a",false,0));Array.Fill(unterminated,(byte)0x61,76,520);Assert.Throws<InvalidDataException>(()=>RdpClipboardFiles.Parse(unterminated));
        var surrogate=Descriptors(("a",false,0));surrogate[76]=0;surrogate[77]=0xd8;Assert.Throws<InvalidDataException>(()=>RdpClipboardFiles.Parse(surrogate));
        var maximum=Descriptors(Enumerable.Range(0,10000).Select(i=>($"root\\f{i}",false,0UL)).ToArray());
        Assert.Equal(10000,RdpClipboardFiles.Parse(maximum).Entries.Count);
        Assert.Throws<InvalidDataException>(()=>RdpClipboardFiles.Parse(new byte[RdpClipboardFiles.MaximumDescriptorBytes+1]));
    }
}
