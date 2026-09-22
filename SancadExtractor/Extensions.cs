using System;

namespace Extractor;

public static class Extensions
{
    /// <summary>
    /// Changes the first and last element of a row should a condition be true
    /// </summary>
    /// <param name="data">A matrix to process.</param>
    /// <param name="condition">The condition that determines if the swap should happen</param>
    /// <typeparam name="T">The type of the elements inside the arrays</typeparam>
    /// <example>
    /// <code>
    /// int[][] matrix = {
    ///     new int[] { 1,2,3 },
    ///     new int[] { 4,5,6 }
    /// };
    ///
    /// int[][] expectedResult = {
    ///     new int[] { 3,2,1 },
    ///     new int[] { 4,5,6 }
    /// };
    ///
    /// matrix.SwapEnd(arr => arr[0] == 1);
    /// Debug.Assert(matrix.SequenceEqual(expectedResult));
    /// </code>
    /// </example>
    public static void SwapEnd<T>(this T[][] data, Func<T[], bool> condition)
    {
        foreach (var t in data)
        {
            if (condition(t))
            {
                (t[0], t[^1]) = (t[^1], t[0]);
            }
        }
    }
       
}