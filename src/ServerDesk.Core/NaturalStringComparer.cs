using System.Globalization;

namespace ServerDesk.Core;

/// <summary>Compares digit runs as integers without parsing or limiting their length.</summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    private readonly CompareInfo? culture;
    private NaturalStringComparer(CompareInfo? culture)=>this.culture=culture;
    public static NaturalStringComparer OrdinalIgnoreCase { get; }=new(null);
    public static NaturalStringComparer CurrentCultureIgnoreCase=>new(CultureInfo.CurrentCulture.CompareInfo);
    private int Text(ReadOnlySpan<char> left,ReadOnlySpan<char> right)=>culture==null?left.CompareTo(right,StringComparison.OrdinalIgnoreCase):culture.Compare(left,right,CompareOptions.IgnoreCase);
    public int Compare(string? left,string? right)
    {
        if(ReferenceEquals(left,right))return 0; if(left==null)return -1; if(right==null)return 1;
        var i=0; var j=0;
        while(i<left.Length && j<right.Length)
        {
            if(char.IsAsciiDigit(left[i]) && char.IsAsciiDigit(right[j]))
            {
                var a=i; var b=j;
                while(i<left.Length && char.IsAsciiDigit(left[i]))i++;
                while(j<right.Length && char.IsAsciiDigit(right[j]))j++;
                var significantA=a; var significantB=b;
                while(significantA<i && left[significantA]=='0')significantA++;
                while(significantB<j && right[significantB]=='0')significantB++;
                var compared=(i-significantA).CompareTo(j-significantB);
                if(compared==0)compared=left.AsSpan(significantA,i-significantA).SequenceCompareTo(right.AsSpan(significantB,j-significantB));
                if(compared==0)compared=(i-a).CompareTo(j-b); // Equal numbers: shortest spelling first.
                if(compared!=0)return compared;
            }
            else
            {
                var a=i; var b=j;
                if(char.IsAsciiDigit(left[i]) || char.IsAsciiDigit(right[j])) { i++; j++; }
                else { while(i<left.Length && !char.IsAsciiDigit(left[i]))i++; while(j<right.Length && !char.IsAsciiDigit(right[j]))j++; }
                var compared=Text(left.AsSpan(a,i-a),right.AsSpan(b,j-b)); if(compared!=0)return compared;
            }
        }
        return (left.Length-i).CompareTo(right.Length-j);
    }
}
