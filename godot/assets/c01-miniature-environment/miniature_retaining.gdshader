shader_type spatial;
render_mode cull_disabled, specular_disabled;

uniform vec4 stone_color : source_color = vec4(0.48, 0.46, 0.40, 1.0);
uniform vec4 mortar_color : source_color = vec4(0.35, 0.36, 0.32, 1.0);
varying vec3 surface_position;
varying vec3 surface_normal;

const float COURSE_HEIGHT = 0.25;
const float BLOCK_LENGTH = 0.66;
const float JOINT_WIDTH = 0.015;

void vertex() {
    surface_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    surface_normal = MODEL_NORMAL_MATRIX * NORMAL;
}

void fragment() {
    float along_wall = abs(surface_normal.z) > abs(surface_normal.x) ? surface_position.x : surface_position.z;
    float course = surface_position.y / COURSE_HEIGHT;
    vec2 blocks = vec2(along_wall / BLOCK_LENGTH + 0.5 * mod(floor(course), 2.0), course);
    vec2 cell = floor(blocks);
    vec2 joint_distance = (0.5 - abs(fract(blocks) - 0.5)) * vec2(BLOCK_LENGTH, COURSE_HEIGHT);
    float seam = min(joint_distance.x, joint_distance.y);
    float filter_width = max(fwidth(seam), 0.004);
    float joint = 1.0 - smoothstep(JOINT_WIDTH - filter_width, JOINT_WIDTH + filter_width, seam);
    float quiet_variation = fract(dot(cell, vec2(0.618034, 0.381966)));
    vec3 stone = stone_color.rgb * mix(0.89, 1.07, quiet_variation);
    ALBEDO = mix(stone, mortar_color.rgb, joint);
    ROUGHNESS = 0.93;
}
