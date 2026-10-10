//! Small deterministic PRNG (xorshift64*), seeded from the clock.
//!
//! Deterministic and seedable so shuffling/selection can be unit-tested
//! without pulling in an external RNG crate.

use std::time::{SystemTime, UNIX_EPOCH};

/// Fallback seed if the clock is unavailable.
const FALLBACK_SEED: u64 = 0x9E37_79B9_7F4A_7C15;
/// Multiplier from the xorshift64* variant.
const MULTIPLIER: u64 = 0x2545_F491_4F6C_DD1D;

pub struct Rng {
    state: u64,
}

impl Rng {
    pub fn from_clock() -> Self {
        let seed = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|elapsed| elapsed.as_nanos() as u64)
            .unwrap_or(FALLBACK_SEED);
        Self::from_seed(seed)
    }

    pub fn from_seed(seed: u64) -> Self {
        // xorshift cannot escape a zero state.
        Self { state: seed | 1 }
    }

    pub fn next_u64(&mut self) -> u64 {
        let mut x = self.state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        self.state = x;
        x.wrapping_mul(MULTIPLIER)
    }

    /// Uniform index in `0..bound`.
    pub fn below(&mut self, bound: usize) -> usize {
        debug_assert!(bound > 0, "below() requires a positive bound");
        let bound = bound as u64;
        // Reject the tail that is not a whole multiple of `bound`, so every
        // index keeps the same probability.
        let accepted = u64::MAX - u64::MAX % bound;
        loop {
            let value = self.next_u64();
            if value < accepted {
                return (value % bound) as usize;
            }
        }
    }

    pub fn shuffle<T>(&mut self, items: &mut [T]) {
        for i in (1..items.len()).rev() {
            let j = self.below(i + 1);
            items.swap(i, j);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn below_stays_in_range() {
        let mut rng = Rng::from_seed(42);
        for bound in [1usize, 2, 7, 100, 1000] {
            for _ in 0..1000 {
                assert!(rng.below(bound) < bound);
            }
        }
    }

    #[test]
    fn shuffle_is_a_permutation() {
        let mut rng = Rng::from_seed(7);
        let mut items: Vec<u32> = (0..100).collect();
        rng.shuffle(&mut items);
        items.sort_unstable();
        assert_eq!(items, (0..100).collect::<Vec<_>>());
    }

    #[test]
    fn same_seed_gives_same_sequence() {
        let mut a = Rng::from_seed(99);
        let mut b = Rng::from_seed(99);
        for _ in 0..50 {
            assert_eq!(a.next_u64(), b.next_u64());
        }
    }
}
