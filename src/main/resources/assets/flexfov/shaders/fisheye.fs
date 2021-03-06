#version 110

#define M_PI 3.14159265

/* This comes interpolated from the vertex shader */
varying vec2 texcoord;

/* The 6 textures to be rendered */
uniform sampler2D texFront;
uniform sampler2D texBack;
uniform sampler2D texLeft;
uniform sampler2D texRight;
uniform sampler2D texTop;
uniform sampler2D texBottom;

uniform int antialiasing;

uniform vec2 pixelOffset[16];

uniform float fovx;
uniform float fovy;

uniform int fisheyeType;
uniform bool fullFrame;

uniform vec4 backgroundColor;

uniform vec2 cursorPos;

uniform bool drawCursor;

uniform float zoom;

vec3 calcZoom(vec3 ray) {
	float z = ray.z - 1.0;
	float temp = zoom*zoom*(ray.x*ray.x + ray.y*ray.y);
	float denom = temp + z*z;
	float numer = temp - z*z;
	return vec3(-2.0*zoom*ray.x*z/denom, -2.0*zoom*ray.y*z/denom, numer/denom);
}

vec4 fisheye(vec3 ray, float x, float y) {
	vec4 color;

	//scale from square view to window shape view //fcontain
	float aspectRatio = fovx/fovy;
	if (aspectRatio > 1.0) {
		x *= aspectRatio;
	} else {
		y /= aspectRatio;
	}

	if (fullFrame) {
		//scale circle radius [1] up to screen diagonal radius [sqrt(2) or higher]
		if (aspectRatio > 1.0) {
			x /= sqrt(aspectRatio*aspectRatio+1.0);
			y /= sqrt(aspectRatio*aspectRatio+1.0);
		} else {
			x /= sqrt((1.0/aspectRatio)*(1.0/aspectRatio)+1.0);
			y /= sqrt((1.0/aspectRatio)*(1.0/aspectRatio)+1.0);
		}
	} else {
		//only draw center circle
		if (x*x+y*y > 1.0) {
			return backgroundColor;
		}
	}

	//max theta as limited by fov
	float fovTheta = fovx*M_PI/360.0;
	float r;
	float theta;
	if (fisheyeType == 4) {//stereographic
		//forward: r=2f*tan(theta/2)
		float maxr = 2.0*tan(fovTheta*0.5);
			x *= maxr;
			y *= maxr;
			r = sqrt(x*x+y*y);
		//inverse:
		theta = 2.0*atan(r*0.5);
	} else if (fisheyeType == 3) {//equidistant
		//This is the x scale of the theta= equation. Not related to fov.
		//it's the result of the forward equation with theta=pi
		//forward: r=f*theta
		float maxr = fovTheta;
			//scale to angle (equidistant) [-1..1] -> [-pi..pi] (orthographic [-0.5..0.5] -> [-pi/2..pi/2]
			x *= maxr;
			y *= maxr;
			//angle from forward <=abs(pi) or <=abs(pi/2)
			r = sqrt(x*x+y*y);
		//inverse:
		theta = r;
	} else if (fisheyeType == 2) {//equisolid
		//forward: r=2f*sin(theta/2)
		float maxr = 2.0*sin(fovTheta*0.5);
			x *= maxr;
			y *= maxr;
			r = sqrt(x*x+y*y);
		//inverse:
		theta = 2.0*asin(r*0.5);
	} else if (fisheyeType == 1) {//thoby
		//it starts shrinking near max fov without this - 256.68 degrees
		fovTheta = min(fovTheta, M_PI*0.713);

		//forward: r=1.47*f*sin(0.713*theta)
		float maxr = 1.47*sin(0.713*fovTheta);
			x *= maxr;
			y *= maxr;
			r = sqrt(x*x+y*y);
		//inverse:
		theta = asin(r/1.47)/0.713;
	} else {// if (fisheyeType == 0) {//orthographic
		//this projection has a mathematical limit at hemisphere
		fovTheta = min(fovTheta, M_PI*0.5);

		//forward: r=f*sin(theta)
		float maxr = sin(fovTheta);
			x *= maxr;
			y *= maxr;
			r = sqrt(x*x+y*y);
		//inverse:
		theta = asin(r);
	}

	//rotate ray
	float s = sin(theta);
	ray = vec3(x/r*s, y/r*s, -cos(theta));
	ray = calcZoom(ray);

	color = vec4(1, 0, 1, 0); //Purple should be obvious if the value is not set below

	//find which side to use\n
	if (abs(ray.x) > abs(ray.y)) {
		if (abs(ray.x) > abs(ray.z)) {
			if (ray.x > 0.0) {
				//right\n
				float x = ray.z / ray.x;
				float y = ray.y / ray.x;
				color = vec4(texture2D(texRight, vec2((x+1.0)/2.0, (y+1.0)/2.0)).rgb, 1.0);
			} else {
				//left\n
				float x = -ray.z / -ray.x;
				float y = ray.y / -ray.x;
				color = vec4(texture2D(texLeft, vec2((x+1.0)/2.0, (y+1.0)/2.0)).rgb, 1.0);
			}
		} else {
			if (ray.z > 0.0) {
				//back\n
				float x = -ray.x / ray.z;
				float y = ray.y / ray.z;
				color = vec4(texture2D(texBack, vec2((x+1.0)/2.0, (y+1.0)/2.0)).rgb, 1.0);
			} else {
				//front\n
				float x = ray.x / -ray.z;
				float y = ray.y / -ray.z;
				color = vec4(texture2D(texFront, vec2((x+1.0)/2.0, (y+1.0)/2.0)).rgb, 1.0);
			}
		}
	} else {
		if (abs(ray.y) > abs(ray.z)) {
			if (ray.y > 0.0) {
				//top\n
				float x = ray.x / ray.y;
				float y = ray.z / ray.y;
				color = vec4(texture2D(texTop, vec2((x+1.0)/2.0, (y+1.0)/2.0)).rgb, 1.0);
			} else {
				//bottom\n
				float x = ray.x / -ray.y;
				float y = -ray.z / -ray.y;
				color = vec4(texture2D(texBottom, vec2((x+1.0)/2.0, (y+1.0)/2.0)).rgb, 1.0);
			}
		} else {
			if (ray.z > 0.0) {
				//back\n
				float x = -ray.x / ray.z;
				float y = ray.y / ray.z;
				color = vec4(texture2D(texBack, vec2((x+1.0)/2.0, (y+1.0)/2.0)).rgb, 1.0);
			} else {
				//front\n
				float x = ray.x / -ray.z;
				float y = ray.y / -ray.z;
				color = vec4(texture2D(texFront, vec2((x+1.0)/2.0, (y+1.0)/2.0)).rgb, 1.0);
			}
		}
	}

	if (drawCursor) {
		vec2 normalAngle = cursorPos*2.0 - 1.0;
		float x = ray.x / -ray.z;
		float y = ray.y / -ray.z;
		if (x <= normalAngle.x + 0.01 && y <= normalAngle.y + 0.01 &&
			x >= normalAngle.x - 0.01 && y >= normalAngle.y - 0.01 &&
			ray.z < 0.0) {
			color = vec4(1.0, 1.0, 1.0, 1.0);
		}
	}
	return color;
}

void main(void) {
  /* Ray-trace a cube */

	//Anti-aliasing
	vec4 colorN[16];

	for (int loop = 0; loop < antialiasing; loop++) {

		//create ray\n
		vec3 ray = vec3(0.0, 0.0, -1.0);

		//point relative to center [0..1] -> [-1..1]
		float x = (texcoord.x+pixelOffset[loop].x);
		float y = (texcoord.y+pixelOffset[loop].y);

		//fisheye stuff
		colorN[loop] = fisheye(ray, x, y);
	}

	if (antialiasing == 16) {
	  vec4 corner[4];
	  corner[0] = mix(mix(colorN[0], colorN[1], 2.0/3.0), mix(colorN[4], colorN[5], 3.0/5.0), 5.0/8.0);
	  corner[1] = mix(mix(colorN[3], colorN[2], 2.0/3.0), mix(colorN[7], colorN[6], 3.0/5.0), 5.0/8.0);
	  corner[2] = mix(mix(colorN[12], colorN[13], 2.0/3.0), mix(colorN[8], colorN[9], 3.0/5.0), 5.0/8.0);
	  corner[3] = mix(mix(colorN[15], colorN[14], 2.0/3.0), mix(colorN[11], colorN[10], 3.0/5.0), 5.0/8.0);
	  gl_FragColor = mix(mix(corner[0], corner[1], 0.5), mix(corner[2], corner[3], 0.5), 0.5);
	}
	else if (antialiasing == 4) {
		gl_FragColor = mix(mix(colorN[0], colorN[1], 0.5), mix(colorN[2], colorN[3], 0.5), 0.5);
	}
	else { //if antialiasing == 1
		gl_FragColor = colorN[0];
	}
}
