﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VsChromium.Core.Collections;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Core.Linq {
  public static class EnumerableExtensions {
    /// <summary>
    /// Partition a list of elements of <paramref name="weight"/> into <paramref
    /// name="partitionCount"/> lists having identical total weight (as best as
    /// possible).
    /// </summary>
    public static IEnumerable<IList<TSource>> PartitionWithWeight<TSource>(
      this IList<TSource> source,
      Func<TSource, long> weight,
      int partitionCount) {
      var description = string.Format("Creating {0} partition(s) for {1} element(s)", partitionCount, source.Count);
      using (new TimeElapsedLogger(description)) {
        var partitions = source
          .GetPartitionSizes(partitionCount)
          .Select(x => new Partition<TSource> {
            MaxCount = x,
            Items = new List<TSource>(x)
          })
          .ToList();

        // Create MinHeap of partitions (wrt Weight function).
        var minHeap = new MinHeap<Partition<TSource>>(new PartitionWeightComparer<TSource>());
        partitions.ForAll(x => minHeap.Add(x));

        // Distribute items using MinHeap.
        source
          // Sort so that elements with highest weight are first, so that we
          // have a better chance of ending with partitions of same total weight
          // - it is easier to adjust with elements of low weight than with
          // elements of high weight.
          .OrderByDescending(weight)
          .ForAll(item => {
            var min = minHeap.Remove();
            min.Items.Add(item);
            min.TotalWeight += weight(item);
            if (min.Items.Count < min.MaxCount)
              minHeap.Add(min);
          });

#if false
      Logger.Log("Created {0} partitions for a total ot {1:n0} elements and weight of {2:n0}.",
        partitions.Count,
        partitions.Aggregate(0L, (c, x) => c + x.Items.Count),
        partitions.Aggregate(0L, (c, x) => c + x.TotalWeight));
      partitions.ForAll(x => {
        Logger.Log("  Partition count is {0:n0}, total weight is {1:n0}.", x.Items.Count, x.TotalWeight);
      });
#endif
        return partitions.Select(x => x.Items);
      }
    }

    private class Partition<T> {
      public int MaxCount { get; set; }
      public List<T> Items { get; set; }
      public long TotalWeight { get; set; }
    }

    private class PartitionWeightComparer<T> : IComparer<Partition<T>> {
      public int Compare(Partition<T> x, Partition<T> y) {
        return x.TotalWeight.CompareTo(y.TotalWeight);
      }
    }

    public static IEnumerable<KeyValuePair<int,int>> GetPartitionRanges<TSource>(
      this IList<TSource> source,
      int partitionCount) {
      var index = 0;
      foreach (var size in source.GetPartitionSizes(partitionCount)) {
        yield return KeyValuePair.Create(index, size);
        index += size;
      }
    }

    public static IEnumerable<int> GetPartitionSizes<TSource>(this IList<TSource> source, int partitionCount) {
      if (partitionCount <= 0)
        throw new ArgumentException("Invalid count", "partitionCount");

      var baseSize = source.Count / partitionCount;
      var additionalItems = source.Count % partitionCount;

      return Enumerable
        .Range(0, partitionCount)
        .Select(i => {
          var actualSize = baseSize;
          if (additionalItems > 0) {
            additionalItems--;
            actualSize++;
          }
          return actualSize;
        });
    }

    public static IList<IList<TSource>> PartitionByChunks<TSource>(this IList<TSource> source, int partitionCount) {
      var sourceIndex = 0;
      return GetPartitionSizes(source, partitionCount).Select(size => {
        IList<TSource> result = new ListSegment<TSource>(source, sourceIndex, size);
        sourceIndex += size;
        return result;
      }).ToList();
    }

    public static void ForAll<TSource>(this IEnumerable<TSource> source, Action<TSource> action) {
      foreach (var x in source) {
        action(x);
      }
    }

    public static void ForAll<TSource>(this IEnumerable<TSource> source, Action<int, TSource> action) {
      var index = 0;
      foreach (var x in source) {
        action(index, x);
        index++;
      }
    }

    public static ReadOnlyCollection<TSource> ToReadOnlyCollection<TSource>(this IEnumerable<TSource> source) {
      var list = source as IList<TSource>;
      if (list != null)
        return new ReadOnlyCollection<TSource>(list);
      return new ReadOnlyCollection<TSource>(source.ToArray());
    }
  }
}
