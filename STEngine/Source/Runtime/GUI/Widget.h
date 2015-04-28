#pragma once

class GUICORE_API GUIControlledConstruction
{
public:
    GUIControlledConstruction(){}
    virtual ~GUIControlledConstruction(){}
private:
    void operator= (const GUIControlledConstruction& Other){}
    void* operator new (const size_t InSize)
    {
        return FMemory::Malloc(InSize);
    }
public:
    void operator delete(void* mem)
    {
        FMemory::Free(mem);
    }
};