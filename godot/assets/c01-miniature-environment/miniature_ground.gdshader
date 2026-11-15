shader_type spatial;
render_mode cull_disabled, specular_disabled;

uniform vec4 soil_color : source_color;
uniform vec4 slab_color : source_color = vec4(0.46, 0.49, 0.47, 1.0);
uniform vec3 market;
uniform bool pave_market;
varying vec3 world_position;

const vec2 RIDGE_LANE_START = vec2(-3.5, -5.8);
const vec2 RIDGE_LANE_LANDING = vec2(-3.5, -0.9);
const vec2 COURT_WEST = vec2(-2.3, 0.0);
const vec2 LOWER_LANE_TURN = vec2(-5.5, 0.0);
const vec2 LOWER_LANE_LANDING = vec2(-5.5, 3.3);
const vec2 LOWER_LANE_END = vec2(-6.1, 6.8);
const float RIDGE_LANE_HALF_WIDTH = 0.71;
const float LOWER_LANE_HALF_WIDTH = 0.64;
const float EDGE_FEATHER = 0.13;
const vec2 SLAB_SIZE = vec2(0.72, 0.44);
const float JOINT_WIDTH = 0.016;

float variation(vec2 cell) {
    vec3 p = fract(vec3(cell.xyx) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

float segment_distance(vec2 p, vec2 start, vec2 end) {
    vec2 line = end - start;
    return length(p - start - line * clamp(dot(p - start, line) / dot(line, line), 0.0, 1.0));
}

float street_edge(vec2 p) {
    float ridge = min(segment_distance(p, RIDGE_LANE_START, RIDGE_LANE_LANDING),
        segment_distance(p, RIDGE_LANE_LANDING, COURT_WEST)) - RIDGE_LANE_HALF_WIDTH;
    float lower = min(segment_distance(p, COURT_WEST, LOWER_LANE_TURN),
        min(segment_distance(p, LOWER_LANE_TURN, LOWER_LANE_LANDING),
            segment_distance(p, LOWER_LANE_LANDING, LOWER_LANE_END))) - LOWER_LANE_HALF_WIDTH;
    vec2 court = abs(p - vec2(-0.25, -0.12)) - vec2(3.05, 0.86);
    float courtyard = length(max(court, vec2(0.0))) + min(max(court.x, court.y), 0.0) - 0.35;
    return min(courtyard, min(ridge, lower));
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
    vec3 terrace_tint = mix(vec3(0.96, 0.91, 0.84), vec3(0.87, 0.94, 0.95), upper_tone);
    vec3 soil = soil_color.rgb * terrace_tint * mix(0.93, 1.03, broad_tone);

    vec2 offset = p - market.xz;
    float edge = street_edge(offset);
    float surface = 1.0 - smoothstep(-EDGE_FEATHER, EDGE_FEATHER, edge);
    float gutter = (1.0 - smoothstep(0.065, 0.16, abs(edge + 0.18))) * surface;

    vec2 stone = offset / SLAB_SIZE;
    stone.x += 0.5 * mod(floor(stone.y), 2.0);
    vec2 joint_distance = (vec2(0.5) - abs(fract(stone) - vec2(0.5))) * SLAB_SIZE;
    float seam = min(joint_distance.x, joint_distance.y);
    float filter_width = max(fwidth(seam), 0.004);
    float joint = 1.0 - smoothstep(JOINT_WIDTH - filter_width, JOINT_WIDTH + filter_width, seam);
    float tone = mix(0.94, 1.06, variation(floor(stone)));
    vec3 slab = slab_color.rgb * tone * (1.0 - 0.12 * joint) * (1.0 - 0.15 * gutter);

    ALBEDO = pave_market ? mix(soil, slab, surface) : soil;
    ROUGHNESS = 0.96;
}
