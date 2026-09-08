using System.Collections.Frozen;
using System.Numerics;

namespace Content.Shared.Weapons.Ranged.Systems;
/// Larger summary of how this works at bottom of page
/// <summary>
///
/// Overly optimized and overly engineered hashing for cartridge dispensing spread
/// When a player cycles a cartridge(from ammo box, a gun ect...) this decides the spread(where cart lands)
/// using bit manipulation on given int values. To make spread seem random in efficent way
/// that totally avoids prediction jitters and such messes
///
/// I DID THIS FOR FUN!!! I DONT MIND IF THIS LATER BECOMES USELESS!!!
/// </summary>
public abstract partial class SharedGunSystem
{

    /// <summary>
    ///
    /// Mapped (N-1) decimal value mapped to => 2^X
    ///
    /// </summary>
    private static Dictionary<int, int> _log2 = new Dictionary<int, int>
    {
        [1] = 1, // avoid 0
        [2] = 1,
        [3] = 2,
        [7] = 3,
        [15] = 4,
        [31] = 5,
        [63] = 6,
        [127] = 7,
        [256] = 8,
        [511] = 9,
        [1023] = 10,
        [2047] = 11,
        [4095] = 12,
        [8191] = 13,
        [16383] = 14,
        [32767] = 15,
        [65535] = 16

    };
    private static FrozenDictionary<int, int> _log2Dict = _log2.ToFrozenDictionary<int, int>();
    /// <summary>
    /// Bit manipulate int to correlate with its rounded down Log2 value as an estimate
    /// that hpefully runs faster than getting its log the normal way
    /// (TODO: use actual bit manip algo to get log10)
    /// ie.. log2(x) = 2^n
    /// we get 2^n(2^n is just nth bit) which then maps to above dict to give us log2(x)
    /// </summary>
    /// <returns> rounded down log2(x) of number </returns>
    /// <remarks>
    /// this is just rough and fast way to get log2 of a number
    /// Doesnt need to be accurate so we keep it simple and fast.
    /// basically we mark every bit preceding the highest order bit
    /// as a way to know what highest order bit is
    /// ie....value of 18 gets bit manip'd to 15 maps=> 16
    ///       value of 19 gets bit manip'd to 15 maps=> 16
    ///       value of 60 gets bit manip'd to 31 maps=> 32
    /// </remarks>
    public static int Log2Floor(int number)
    {
        number |= number >> 1;
        number |= number >> 2;
        number |= number >> 4;
        number |= number >> 8;
        number |= number >> 16;
        return _log2Dict.GetValueOrDefault(number, 16);
    }

    private const int ExpoBaseMask = 0b0_0111_1000_0000_0000_0000_0000_0000_000;
    private const int ExpoPattern = 0b0010_1010_1010_1100_0011_0100_0010_1010;
    private const int ExpoPatternMask = 0b0000_0111_0000_0000_0000_0000_0000_000;
    private const int MantissaMask = 0b00000000000000000000000001111111;
    private const int ShiftRightOffset = 16;
    // Pi/12
    private const float PiOvrTwelve = 0.2618f;
    // accurate float value 0011111....
    // if ur not a nerd floats lie to you, they are just huge combos of binary
    // that roughly estimate to a desired value. They are decent at lying, having 23 bits to do so
    // if you do not consider the sign and exponent bits
    private const float FloatMinConst = 0.125f; // min float to stop angle from settling on 0(does this alot lol)
    /// <summary>
    /// "random" local position and rotation by some hashed algorithim given a sequence and seed
    /// seed needs to be something that's the same between client and server(use a spawned thing's netID)
    /// seed prevents jitters/desync
    /// </summary>
    /// <param name="seed">seed value that's the SAME ON SERVER AND CLIENT(usually just NetEnt.Id)</param>
    /// <param name="sequence">sequence/count that corresponds to nth call</param>
    /// <remarks> Should be super fast with little overhead(dont call log, dont do division, no modulo, only fast cpu math)
    /// Summary of how this REALLY works at bottom of page
    public static (Vector2, Angle) GetRandVectAngle(int seed, int sequence)
    {
        // casting should be free
        // roughly equiv to n*log(n) which roughly is the nth prime number. Makes stuff seem randomerrr
        uint primeApprox = (uint) (sequence * Log2Floor(sequence));

        // shift/rotate bit pattern by count. Rotate is circular version of shift
        // ex.. 1010_1010_1010_1100_0011_0100_0010_1010 (shift by 1) => 0101_0101_0101_1000_0110_1000_0101_0101
        // we apply shifted ExpoPattern here with ExpoBaseMask
        var fullExpoBits = (BitOperations.RotateLeft(ExpoPattern, sequence) & ExpoPatternMask) | ExpoBaseMask;

        // right shift primeApprox to fit inside mantissa(by num trailingzero) and
        // also so we can left shift it to same place everytime
        // bits for float assembled here
        var float_bits = (((primeApprox >> BitOperations.TrailingZeroCount(primeApprox)) & MantissaMask) << ShiftRightOffset)
                            | fullExpoBits;
        // we turn those 32Uint bits into 32float bits (really stupid but whatever. wish there was a pure bit manip mode)
        var radius = BitConverter.UInt32BitsToSingle(float_bits);

        // casting should be free
        uint theta = primeApprox + (uint) seed;

        var x = radius * MathF.Cos(theta);
        var y = radius * MathF.Sin(theta);
        var pos = new Vector2(x, y);
        var rot = new Angle(primeApprox * PiOvrTwelve + FloatMinConst);
        return (pos, rot);
    }

}

/// summary of how hash works!!!!!! skim through or skip if you do not need stuff
/// like bit manipulation, IEEE floating point standard, or ect explained
/// mostly done for fun
/// (ik for some this is painfully basic or overly complicated to do a basic thing or both)
///

/// everything is mostly 32 bits, including count and netEnt.ID
///
/// So we apply a cheap/rough calc to estimate nth prime numbers
/// prime = 0000_0000_0000_0000_0000_0000_0xxx_xxxx : x is whatever values prime is
/// we restrict value to those x's with a mask so anything after x's are 0
///
/// then that prime's bits are shifted from being right most 7th to
/// left most 7th bits of a float's mantissa. The float's bits has its own masks
///  and things done to it shown below
/// specifcally shifted to "raw" float value named the mantissa
/// float = 0___01111zzz_____xxx_xxxx_0000_0000_0000_0000
///     sigh^  ^exponent^bits  ^mantissa bits^
///     1 bit   8 bits           23 bits(restrict prime value to be left most 7th bits)
///
/// z => cycling bit pattern that moves/cycles each count.
///      Pattern only applied to bits covered by z
/// 1's next to z around the end are the base exponent value. They never change and have its own
/// mask to ensure that
///
/// SO!!! we just use that patchwork of bits to get a float we can use. Yes this is
/// an overly complicated way to hash some value. Doing it this way is faster than most normal methods(I hope lol)
/// since the idea is that we are only doing bit manipulation and basic adding/substraction
/// which use way less CPU cycles than something like division.
/// We also avoid CPU cycles from having to calculate a float by just assembling its
/// bits manually in our own way which also acts as our hash(two birds one stone yay)
///
/// since I am already explaining what a float kinda is, might as well also explain masks(cause my dumbass comp sci teacher wouldnt and I ended up learning it in my third year from a random yt video)
/// a bit mask here is just another 32 bit value we use to do an AND(&) operation on another 32 bit value
/// We do this to "filter" specific bits and positions.
/// Ie... we randomly get 8 bit values xxxx_xxxx of anything but we only want the first 4 bits
/// so we use the mask var mask = 0000_1111 to do... mask & xxxx_xxxx => 0000_xxxx
/// so now we can do whatever we want with those 4 bits without worrying about the rest
///
/// Also when I say hash I mean some input of values that feed into some algo that maps to some other value
/// I personally think of it as a sophisticated Rube Goldberg machine
/// <summary>
/// This does have a practical use case since we cant just use robustRandom to get rng spread.
/// Jitters will happen since prediction cant predict randomness, so we need something that both client and server
/// can trust on, ie an algorithim that has determinalistic results given inputs client/server can agree on
///
/// Cannot use a static rng instance as a field here since client prediction will call its own
/// instance multiple times more than server from prediction. So why not turn off prediction?
/// Not like we need to predict something simple multiple times. Issue...
///
/// doesnt solve loss/late packets to the server, which will happen when you're spam clicking Z
/// on an ammo cart. Ie... 50th wasnt detected by server, so that will cause cycles after that
/// to be desynced with server's RNG instance. Also forgetting how this would work with multiple players
/// Could have a local rng instance made for every call but that isnt preformant(at least for me lol)
///
/// Infact, even things like time/ticks dsyncs easily between server/client so jitters still happen.
/// we need values where desync is irrelavent or hardly happens and a way to
/// hash/alter values to make random seeming spread in some predictable way
///
/// Solution here was to use the premise of ammoCarts. We always eject ammo 1 by 1
/// that gives us a very predictable input and a sequencer we can use to cycle a pattern.
/// Can't just use that to seed our hash tho, since EVERY ammo cart counts down obviously lol
/// so we also add the EntID value of each cart as a seed
///
/// We can trust EntID to hardly ever be desynced since we are handling ejecting ammo, not spawning the thing
/// Most cases we can assume the EntID has been agreed on by both client and server. So every ammo cart technhcially has a
/// unique sequence/pattern when ejecting ammo that both the client and server can predict
///
/// Even if one input is missed via a bunch of spam clicks, it'll eject the same without jitter
/// since its based on the current ammo left in the cart, not how many times you spam clicked the box
/// spam clicking will actually hide that desync ie...
/// (50th eject packet lost, but 51th eject still listened to, which will take the 50th's place being the 50th cartridge)
///
/// /// //This is what happens when you let an autist try applying what they learned from their
/// //comp architecture class into their autism game
/// </summary>


