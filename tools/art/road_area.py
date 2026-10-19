"""Bounded road alpha geometry; occupancy owns shape, never material sampling."""
from functools import lru_cache

import numpy as np
from PIL import Image


TILE_SIZE = 32
INSET = 5
CONVEX_RADIUS = 9
CONCAVE_RADIUS = 5
SUPERSAMPLE = 16
BIT_ORDER = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW']
GEOMETRY = 'external-inset-rounded-convex-concave-v1'


def road_coverage(mask8, x, y):
    """Evaluate the closed analytic contour in one occupied logical cell.

    Shared sides are never inset. Two exposed sides form a radius-nine convex
    corner; two present sides with an absent diagonal form a radius-five notch.
    Diagonals alone cannot create a connection. Clipping to the central square
    is unconditional, including when a bridge supplies a connection neighbor.
    """
    if type(mask8) is not int or not 0 <= mask8 <= 255:
        raise ValueError('expected an eight-bit integer occupancy mask')
    x, y = np.broadcast_arrays(np.asarray(x), np.asarray(y))
    n, ne, e, se, south, sw, w, nw = [bool(mask8 & (1 << i)) for i in range(8)]
    left, top = (0 if w else INSET), (0 if n else INSET)
    right, bottom = (TILE_SIZE if e else TILE_SIZE - INSET), (TILE_SIZE if south else TILE_SIZE - INSET)
    covered = (x >= left) & (x <= right) & (y >= top) & (y <= bottom)
    corners = [
        (n, w, nw, left, top, 1, 1, 0, 0),
        (n, e, ne, right, top, -1, 1, TILE_SIZE, 0),
        (south, e, se, right, bottom, -1, -1, TILE_SIZE, TILE_SIZE),
        (south, w, sw, left, bottom, 1, -1, 0, TILE_SIZE),
    ]
    for a, b, diagonal, edge_x, edge_y, dx, dy, vertex_x, vertex_y in corners:
        if not a and not b:
            cx, cy = edge_x + dx * CONVEX_RADIUS, edge_y + dy * CONVEX_RADIUS
            corner = (dx * (x - cx) < 0) & (dy * (y - cy) < 0)
            covered &= ~corner | ((x - cx) ** 2 + (y - cy) ** 2 <= CONVEX_RADIUS ** 2)
        elif a and b and not diagonal:
            covered &= (x - vertex_x) ** 2 + (y - vertex_y) ** 2 >= CONCAVE_RADIUS ** 2
    return covered


@lru_cache(maxsize=256)
def _supersampled_coverage(mask8):
    # Both output densities integrate this identical, world-anchored sample grid.
    coordinates = (np.arange(TILE_SIZE * SUPERSAMPLE) + .5) / SUPERSAMPLE
    result = road_coverage(mask8, coordinates[None, :], coordinates[:, None])
    result.flags.writeable = False
    return result


def road_area_mask(size, mask8):
    if type(size) is not int or size not in (32, 64):
        raise ValueError('expected a 32px or 64px road mask')
    if type(mask8) is not int or not 0 <= mask8 <= 255:
        raise ValueError('expected an eight-bit integer occupancy mask')
    factor = TILE_SIZE * SUPERSAMPLE // size
    samples = _supersampled_coverage(mask8).reshape(size, factor, size, factor)
    count = samples.sum(axis=(1, 3), dtype=np.uint32)
    alpha = ((count * 255 + factor * factor // 2) // (factor * factor)).astype(np.uint8)
    return Image.fromarray(alpha)
