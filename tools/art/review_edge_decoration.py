"""Check actual rendered edge alpha against roads, bridge landings and initial feet."""
import math
from PIL import Image, ImageChops, ImageDraw


def compare_edge_crop(expected, actual):
    alpha_difference = ImageChops.difference(expected.getchannel('A'), actual.getchannel('A')).getextrema()[1]
    premultiplied_difference = max(ImageChops.difference(
        ImageChops.multiply(expected.getchannel(channel), expected.getchannel('A')),
        ImageChops.multiply(actual.getchannel(channel), actual.getchannel('A'))).getextrema()[1]
        for channel in ('R', 'G', 'B'))
    if alpha_difference > 2 or premultiplied_difference > 2:
        raise ValueError('authored edge crop exceeded fractional-raster tolerance')
    return {'maximumAlphaDifference': alpha_difference,
            'maximumPremultipliedColorDifference': premultiplied_difference,
            'rasterTolerance': 2,
            'note': 'World registrations are unchanged; independently rasterized fractional images allow at most 2/255 alpha and premultiplied color difference. Near-transparent raw RGB is reported but is not a coverage change.'}


def validate_edges(directory, provenance):
    safety = provenance['edgeSafety']
    threshold = safety['meaningfulAlphaMin']
    if threshold != 2 or provenance['unitCount'] != 20 or provenance['buildingCount'] != 9:
        raise ValueError('incomplete authored edge safety evidence')
    results = []
    water = {tuple(cell) for cell in provenance['waterAndBridgeCells']}
    for density in (1, 2):
        alpha = Image.open(directory / f'field-full@{density}x.png').convert('RGBA').getchannel('A')
        bank = Image.open(directory / f'bank-only-full@{density}x.png').convert('RGBA').getchannel('A')
        road = Image.open(directory / f'road-core-full@{density}x.png').convert('RGBA').getchannel('A')
        size = tuple(n * 32 * density for n in provenance['mapSize'])
        if any(image.size != size for image in (alpha, bank, road)):
            raise ValueError('edge safety image dimensions differ from the complete map')
        road = road.point(lambda a: 255 if a == 255 else 0)
        bridge_mask = Image.new('L', size)
        feet_mask = Image.new('L', size)
        deep_water = Image.new('L', size)

        def rectangle(mask, box):
            x0, y0, x1, y1 = [n * density for n in box]
            ImageDraw.Draw(mask).rectangle((math.floor(x0), math.floor(y0), math.ceil(x1) - 1, math.ceil(y1) - 1), fill=255)

        for x, y in safety['bridgeCells']:
            # Horizontal c01-02 bridge core plus the neighbouring landing cells.
            rectangle(bridge_mask, [(x - 1) * 32, y * 32, (x + 2) * 32, (y + 1) * 32])
        half_x, half_y = safety['footHalfExtent']
        for x, y in safety['protectedInitialFeet']:
            rectangle(feet_mask, [x - half_x, y - half_y, x + half_x, y + half_y])
        for x, y in water:
            inset = safety['maximumMeaningfulBankOverhang']
            left = x * 32 + (inset if (x - 1, y) not in water else 0)
            right = (x + 1) * 32 - (inset if (x + 1, y) not in water else 0)
            rectangle(deep_water, [left, y * 32, right, (y + 1) * 32])
        maxima = {
            'roadCore': ImageChops.multiply(alpha, road).getextrema()[1],
            'bridgeCoreAndLandings': ImageChops.multiply(alpha, bridge_mask).getextrema()[1],
            'initialActorFeet': ImageChops.multiply(alpha, feet_mask).getextrema()[1],
            'bankBeyondNearWaterBand': ImageChops.multiply(bank, deep_water).getextrema()[1],
        }
        if any(value >= threshold for value in maxima.values()):
            raise ValueError(f'edge decoration safety overlap at {density}x: {maxima}')
        results.append({'density': density, 'maximumAlphaInProtectedAreas': maxima,
                        'meaningfulAlphaMin': threshold, 'allProtectedAreasClearAtMeaningfulAlpha': True})
    return {'checks': results, 'protectedInitialActorCount': len(safety['protectedInitialFeet']),
            'maximumMeaningfulBankOverhang': safety['maximumMeaningfulBankOverhang'],
            'alpha1Residue': 'Preserved; reported separately from coverage at alpha >= 2/255',
            'roadMask': 'Actually rendered unchanged route layer, opaque core pixels, including settlement connectors',
            'bridgeMask': 'Actual horizontal bridge cells and their one-cell left/right landings',
            'coverage': safety['coverage'], 'movingActors': safety['movingActors']}
