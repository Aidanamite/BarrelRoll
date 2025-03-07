using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class SimpleWave
{
    double[] _x;
    double[] _y;

    public SimpleWave(params (double x, double y)[] positions)
    {
        if (positions != null && positions.Length < 1)
            throw new ArgumentNullException("Cannot be null or empty", "positions");
        bool sort = false;
        var last = positions[0];
        for (int i = 1; i < positions.Length; i++)
        {
            var curr = positions[i];
            if (curr.x < last.x)
            {
                sort = true;
                break;
            }
            last = curr;
        }
        if (sort)
            Array.Sort(positions, (a, b) => a.x.CompareTo(b.x));
        _x = new double[positions.Length * 2 - 1];
        _y = new double[positions.Length * 2 - 1];
        for (int j = 0; j < positions.Length; j++)
        {
            var curr = positions[j];
            _x[j * 2] = curr.x;
            _y[j * 2] = curr.y;
            if (j != 0)
            {
                _x[j * 2 - 1] = (last.x - curr.x) / 2.0 + curr.x;
                _y[j * 2 - 1] = (last.y - curr.y) / 2.0 + curr.y;
            }
            last = curr;
        }
    }

    public double Sample(double x)
    {
        double result;
        if (_x[0] >= x)
            result = _y[0];
        else if (_x[_x.Length - 1] <= x)
            result = _y[_x.Length - 1];
        else
        {
            var ind = Array.BinarySearch(_x, x);
            if (ind >= 0)
                result = _y[ind];
            else
            {
                ind = ~ind;
                var px = _x[ind - 1];
                var dx = _x[ind] - px;
                var py = _y[ind - 1];
                var dy = _y[ind] - py;
                result = dy * (((ind & 1) == 1) ? (1 - Math.Cos((x - px) / dx * Math.PI / 2)) : Math.Sin((x - px) / dx * Math.PI / 2)) + py;
            }
        }
        return result;
    }
}