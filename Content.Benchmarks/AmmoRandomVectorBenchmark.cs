using BenchmarkDotNet.Attributes;
using Content.Client.IoC;
using Robust.Shared.Analyzers;
using Robust.Shared.Maths;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Content.Benchmarks;
/// <summary>
///
/// different iterations and tests for code in <see cref="SharedGunSystem.Ballistics.CartSpread"/>
/// Not up to date with latest iteration(ill get to it i just need to actually PR something first)
///
/// Done for fun and done to learn how to use .Net's benchmarker!!!
/// These are just microbenchmark tests!!!!
///
/// where things are different by at most like 10 nanoseconds and allocate at most 80 Bytes
/// "longest" result was like 20 nanoseconds
/// This is for bullet cycling which is triggered INDIVIDUALLY BY PLAYER SPAM CLICKING LOL
/// it isn't a super CPU intentsive process if it is bottlenecked by input limits or human finger speed
///
/// </summary>
[DisassemblyDiagnoser]
[MemoryDiagnoser]
[Virtual]
public class AmmoRandomVectorBenchmark
{

    private const float Pi = 3.141f;

    private static Random _rng = new Random(100);
    public static IEnumerator<int> GetID()
    {
        yield return _rng.Next(0, 10000);

    }


    public static int NetID
    {
        get
        {

            var i = GetID().Current;
            GetID().MoveNext();
            return i;

        }
    }

    public static IEnumerator<int> GetCount()
    {
        int j = 1;
        while (true)
            yield return j++;

    }



    [Params(true)]
    public bool Run;


    [GlobalSetup]
    public void Setup()
    {
        Random rand = new Random(100);
        for (int i = 0; i < NEnt; i++)
        {
            CountALT.Append(i % 60);
        }

        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 100; j++)
            {
                NetIDAlt.Append(rand.Next(1, 10000));
            }
        }


    }

    [Benchmark]
    public (Vector2, Angle) RandomVectorBitManipulation()
    {
        if (!Run)
        {

            return (Vector2.Zero, Angle.Zero);
        }
        // even more autistic hashing
        var count = GetCount().Current;
        GetCount().MoveNext();


        var thing = (count >> 1) + (count << BitOperations.TrailingZeroCount(count));

        var radius = MathF.ReciprocalEstimate((((thing) & 120) >> 4) | 4);

        var theta = thing | (NetID >> 1);
        var x = MathF.Round(radius * MathF.Cos(theta), 4);
        var y = MathF.Round(radius * MathF.Sin(theta), 4);

        Vector2 position = new Vector2(x, y);
        Angle rotation = new Angle(thing * (Pi / 6));
        return (position, rotation);
        // _xform.SetLocalPositionRotation(spawnedEntUID, pos, rot);
    }

    [Benchmark]
    [Arguments(30, 1234)]
    [Arguments(1200, 1234567)]
    public (Vector2, Angle) RandomVectorBitManipulationArgs(int cnt, int id)
    {

        if (!Run)
        {

            return (Vector2.Zero, Angle.Zero);
        }
        // even more autistic hashing



        var thing = (cnt >> 1) + (cnt << BitOperations.TrailingZeroCount(cnt));

        var radius = MathF.ReciprocalEstimate((((thing) & 120) >> 4) | 4);

        var theta = thing | (id >> 1);
        var x = MathF.Round(radius * MathF.Cos(theta), 4);
        var y = MathF.Round(radius * MathF.Sin(theta), 4);

        Vector2 position = new Vector2(x, y);
        Angle rotation = new Angle(thing * (Pi / 6));
        return (position, rotation);
        // _xform.SetLocalPositionRotation(spawnedEntUID, pos, rot);
    }

    [Benchmark]
    [Arguments(30, 1234)]
    [Arguments(1200, 1234567)]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public (Vector2, Angle) RandomVectorBitManipulationArgsOpti(int cnt, int id)
    {

        if (!Run)
        {

            return (Vector2.Zero, Angle.Zero);
        }
        // even more autistic hashing



        var thing = (cnt >> 1) + (cnt << BitOperations.TrailingZeroCount(cnt));

        var radius = MathF.ReciprocalEstimate((((thing) & 120) >> 4) | 4);

        var theta = thing | (id >> 1);
        var x = MathF.Round(radius * MathF.Cos(theta), 4);
        var y = MathF.Round(radius * MathF.Sin(theta), 4);

        Vector2 position = new Vector2(x, y);
        Angle rotation = new Angle(thing * (Pi / 6));
        return (position, rotation);
        // _xform.SetLocalPositionRotation(spawnedEntUID, pos, rot);
    }
    [Benchmark]
    [Arguments(30, 1234)]
    [Arguments(1200, 1234567)]
    public static (Vector2, Angle) RandomVectorBitManipulationArgsTryHard(int cnt, int id)
    {

        // even more autistic hashing
        var thing = (cnt >> 1) + (cnt << BitOperations.TrailingZeroCount(cnt));

        var radius = MathF.ReciprocalEstimate((((thing) & 120) >> 4) | 4);

        // var theta = thing | (id >> 1);
        // var x = MathF.Round(radius * MathF.Cos(thing | (id >> 1)), 4);
        // var y = MathF.Round(radius * MathF.Sin(thing | (id >> 1)), 4);

        //Vector2 position = new Vector2(MathF.Round(radius * MathF.Cos(thing | (id >> 1)), 4), MathF.Round(radius * MathF.Sin(thing | (id >> 1)), 4));
        //Angle rotation = new Angle(thing * (Pi / 6));
        return (new Vector2(MathF.Round(radius * MathF.Cos(thing | (id >> 1)), 4), MathF.Round(radius * MathF.Sin(thing | (id >> 1)), 4)), new Angle(thing * (Pi / 6)));
        // _xform.SetLocalPositionRotation(spawnedEntUID, pos, rot);
    }
    [Params(1000)] public static int NEnt { get; set; }

    public List<int> NetIDAlt = new List<int>(NEnt);


    public List<int> CountALT = new List<int>(NEnt);




    [Benchmark]
    public (Vector2, Angle) RandomVectorBitManipulationBigLoop()
    {
        Vector2 position = Vector2.Zero;
        Angle rotation = Angle.Zero;
        foreach (var (net, cnt) in NetIDAlt.Zip(CountALT))
        {


            if (!Run)
            {
                position = Vector2.Zero;
                rotation = Angle.Zero;
                continue;
            }
            // even more autistic hashing
            var count = cnt;

            var primeApprox = (count >> 1) + (count << BitOperations.TrailingZeroCount(count));

            var radius = MathF.ReciprocalEstimate((((primeApprox) & 120) >> 4) | 4);

            var theta = primeApprox | (net >> 1);
            var x = MathF.Round(radius * MathF.Cos(theta), 4);
            var y = MathF.Round(radius * MathF.Sin(theta), 4);

            position = new Vector2(x, y);
            rotation = new Angle(primeApprox * (Pi / 6));
        }
        return (position, rotation);
        // _xform.SetLocalPositionRotation(spawnedEntUID, pos, rot);
    }

    [Benchmark]
    public (Vector2, Angle) RandomVectorClassico()
    {

        if (!Run)
        {

            return (Vector2.Zero, Angle.Zero);
        }

        // autistic hashing
        var count = GetCount().Current;
        GetCount().MoveNext();
        int primeAprox = (int) (count * MathF.Log(count));
        var theta = primeAprox * NetID;
        var radius = (primeAprox % 4) / 10;
        int primeAprox2 = (int) ((3 + count) * MathF.Log(3 + count));

        var x = MathF.Round(radius * MathF.Cos(theta), 4);
        var y = MathF.Round(radius * MathF.Sin(theta), 4);

        Vector2 position = new Vector2(x, y);
        Angle rotation = new Angle(primeAprox2 * (Pi / 6));
        return (position, rotation);
        // _xform.SetLocalPositionRotation(spawnedEntUID, pos, rot);
    }

    [Benchmark]
    [Arguments(30, 1234)]
    [Arguments(1200, 1234567)]
    public (Vector2, Angle) RandomVectorClassicoArgs(int cnt, int id)
    {

        if (!Run)
        {

            return (Vector2.Zero, Angle.Zero);
        }

        // autistic hashing

        int primeAprox = (int) (cnt * MathF.Log(cnt));
        var theta = primeAprox * id;
        var radius = (primeAprox % 4) / 10;
        int primeAprox2 = (int) ((3 + cnt) * MathF.Log(3 + cnt));

        var x = MathF.Round(radius * MathF.Cos(theta), 4);
        var y = MathF.Round(radius * MathF.Sin(theta), 4);

        Vector2 position = new Vector2(x, y);
        Angle rotation = new Angle(primeAprox2 * (Pi / 6));
        return (position, rotation);
    }

    [Benchmark]
    [Arguments(30, 1234)]
    [Arguments(1200, 1234567)]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public (Vector2, Angle) RandomVectorClassicoArgsOpti(int cnt, int id)
    {

        if (!Run)
        {

            return (Vector2.Zero, Angle.Zero);
        }

        // autistic hashing

        int primeAprox = (int) (cnt * MathF.Log(cnt));
        var theta = primeAprox * id;
        var radius = (primeAprox % 4) / 10;
        int primeAprox2 = (int) ((3 + cnt) * MathF.Log(3 + cnt));

        var x = MathF.Round(radius * MathF.Cos(theta), 4);
        var y = MathF.Round(radius * MathF.Sin(theta), 4);

        Vector2 position = new Vector2(x, y);
        Angle rotation = new Angle(primeAprox2 * (Pi / 6));
        return (position, rotation);
    }

    [Benchmark]
    [Arguments(30, 1234)]
    [Arguments(1200, 1234567)]
    public static (Vector2, Angle) RandomVectorClassicoArgsTryHard(int cnt, int id)
    {

        // autistic hashing

        int primeAprox = (int) (cnt * MathF.Log(cnt));
        //var theta = primeAprox * id;
        var radius = (primeAprox % 4) / 10;
        //int primeAprox2 = (int) ((3 + cnt) * MathF.Log(3 + cnt));

        //var x = MathF.Round(radius * MathF.Cos(primeAprox * id), 4);
        //var y = MathF.Round(radius * MathF.Sin(primeAprox * id), 4);

        //Vector2 position = new Vector2(x, y);
        //Angle rotation = new Angle(((3 + cnt) * MathF.Log(3 + cnt)) * (Pi / 6));
        return (new Vector2(MathF.Round(radius * MathF.Cos(primeAprox * id), 4), MathF.Round(radius * MathF.Sin(primeAprox * id), 4)), new Angle(((3 + cnt) * MathF.Log(3 + cnt)) * (Pi / 6)));
    }

    [Benchmark]
    public (Vector2, Angle) RandomVectorClassicoBigLoop()
    {
        Vector2 position = Vector2.Zero;
        Angle rotation = Angle.Zero;
        foreach (var (net, cnt) in NetIDAlt.Zip(CountALT))
        {

            if (!Run)
            {
                position = Vector2.Zero;
                rotation = Angle.Zero;
                continue;
            }

            // autistic hashing
            var count = cnt;
            int primeAprox = (int) (count * MathF.Log(count));
            var theta = primeAprox * net;
            var radius = (primeAprox % 4) / 10;
            int primeAprox2 = (int) ((3 + count) * MathF.Log(3 + count));

            var x = MathF.Round(radius * MathF.Cos(theta), 4);
            var y = MathF.Round(radius * MathF.Sin(theta), 4);

            position = new Vector2(x, y);
            rotation = new Angle(primeAprox2 * (Pi / 6));
        }
        return (position, rotation);
        // _xform.SetLocalPositionRotation(spawnedEntUID, pos, rot);
    }



}
