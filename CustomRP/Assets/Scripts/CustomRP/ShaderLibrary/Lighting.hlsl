#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED


float3 IncomingLight(Surface surface, Light light) {
	return saturate(dot(surface.normal, light.direction) * light.attenuation) * light.color;
}

float3 GetLighting(Surface surfaceWS, BRDF brdf, Light light) {
	return IncomingLight(surfaceWS, light) * DirectBRDF(surfaceWS, brdf, light);
}

float3 GetLighting(Surface surfaceWS, BRDF brdf, GI gi) {
	float3 color = gi.diffuse * brdf.diffuse;
	ShadowData shadowData = GetShadowData(surfaceWS);
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		Light light = GetDirectionalLight(i, surfaceWS, shadowData); 
		color += GetLighting(surfaceWS, brdf, light);
	}
	return color;
}	
#endif

