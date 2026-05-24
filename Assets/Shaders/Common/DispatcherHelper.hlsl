#ifndef _SHADERS_CS_DISPATCHERHELPER_HLSL
#define _SHADERS_CS_DISPATCHERHELPER_HLSL

uint _DispatchedX;
uint _DispatchedY;
uint _DispatchedZ;

#define RETURN_IF_INVALID(TID) if(any(TID >= uint3(_DispatchedX, _DispatchedY,_DispatchedZ))) return;


#endif
