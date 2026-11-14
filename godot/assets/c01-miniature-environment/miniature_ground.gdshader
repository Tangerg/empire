shader_type spatial;
render_mode cull_disabled, specular_disabled;

uniform vec4 soil_color : source_color;
uniform vec4 slab_color : source_color = vec4(0.50, 0.47, 0.43, 1.0);
uniform vec3 market;
uniform bool pave_market;
varying vec3 world_position;

float variation(vec2 cell) {
    vec3 p = fract(vec3(cell.xyx) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec2 p = world_position.xz;
    vec2 patch = p * vec2(0.8, 1.0);
    vec2 cell = floor(patch);
    vec2 f = smoothstep(vec2(0.0), vec2(1.0), fract(patch));
    float broad_tone = mix(mix(variation(cell), variation(cell + vec2(1.0, 0.0)), f.x),
        mix(variation(cell + vec2(0.0, 1.0)), variation(cell + vec2(1.0)), f.x), f.y);
    float upper_tone = smoothstep(0.10, 1.45, world_position.y);
    vec3 terrace_tint = mix(vec3(0.96, 0.88, 0.79), vec3(0.84, 0.94, 0.98), upper_tone);
    vec3 soil = soil_color.rgb * terrace_tint * mix(0.87, 1.04, broad_tone);

    vec2 offset = p - market.xz;
    vec2 extents = abs(offset) / vec2(4.6, 1.44);
    float distance_to_edge = max(extents.x, extents.y) + (broad_tone - 0.5) * 0.06;
    float courtyard = (1.0 - smoothstep(0.73, 1.02, distance_to_edge))
        * (1.0 - smoothstep(0.07, 0.31, abs(world_position.y - market.y)));

    vec2 stone = offset * vec2(1.5, 2.4);
    stone.x += 0.5 * mod(floor(stone.y), 2.0);
    vec2 block = abs(fract(stone) - 0.5);
    float joint = smoothstep(0.43, 0.49, max(block.x, block.y));
    float tone = mix(0.86, 1.12, variation(floor(stone)));
    vec3 slab = slab_color.rgb * tone * (1.0 - 0.14 * joint);

    ALBEDO = pave_market ? mix(soil, slab, courtyard * 0.92) : soil;
    ROUGHNESS = 0.96;
}
