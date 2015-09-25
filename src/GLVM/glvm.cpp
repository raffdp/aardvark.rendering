#ifndef __GNUC__
#include "stdafx.h"
#endif

#include "State.h"
#include "glvm.h"

#ifdef __GNUC__

static void* getProc(const char* name)
{
	void* ptr = (void*)glXGetProcAddressARB((const GLubyte*)name);
	if(ptr == nullptr)
		printf("could not import function %s\n", name);

	printf("function address for %s: %lX\n", name, (unsigned long int)ptr);

	return ptr;
}


#else

static PROC getProc(LPCSTR name)
{
	auto ptr = wglGetProcAddress(name);

	if (ptr == nullptr)
		printf("could not import function %s\n", name);

	return ptr;
}

#endif

static bool initialized = false;

DllExport(void) vmInit()
{
	//printf("asdasd\n");
	if (initialized)
		return;

	initialized = true;

	#ifndef __GNUC__
	glActiveTexture = (PFNGLACTIVETEXTUREPROC)getProc("glActiveTexture");
	glBlendColor = (PFNGLBLENDCOLORPROC)getProc("glBlendColor");
	#endif

	glBindVertexArray = (PFNGLBINDVERTEXARRAYPROC)getProc("glBindVertexArray");
	glUseProgram = (PFNGLUSEPROGRAMPROC)getProc("glUseProgram");
	glBindSampler = (PFNGLBINDSAMPLERPROC)getProc("glBindSampler");
	glBindBufferBase = (PFNGLBINDBUFFERBASEPROC)getProc("glBindBufferBase");
	glBindBufferRange = (PFNGLBINDBUFFERRANGEPROC)getProc("glBindBufferRange");
	glBindFramebuffer = (PFNGLBINDFRAMEBUFFERPROC)getProc("glBindFramebuffer");
	glBlendFuncSeparate = (PFNGLBLENDFUNCSEPARATEPROC)getProc("glBlendFuncSeparate");
	glBlendEquationSeparate = (PFNGLBLENDEQUATIONSEPARATEPROC)getProc("glBlendEquationSeparate");
	glStencilFuncSeparate = (PFNGLSTENCILFUNCSEPARATEPROC)getProc("glStencilFuncSeparate");
	glStencilOpSeparate = (PFNGLSTENCILOPSEPARATEPROC)getProc("glStencilOpSeparate");
	glPatchParameteri = (PFNGLPATCHPARAMETERIPROC)getProc("glPatchParameteri");
	glDrawArraysInstanced = (PFNGLDRAWARRAYSINSTANCEDPROC)getProc("glDrawArraysInstanced");
	glDrawElementsInstanced = (PFNGLDRAWELEMENTSINSTANCEDPROC)getProc("glDrawElementsInstanced");
	glVertexAttribPointer = (PFNGLVERTEXATTRIBPOINTERPROC)getProc("glVertexAttribPointer");
	glUniform1fv = (PFNGLUNIFORM1FVPROC)getProc("glUniform1fv");
	glUniform1iv = (PFNGLUNIFORM1IVPROC)getProc("glUniform1iv");
	glUniform2fv = (PFNGLUNIFORM2FVPROC)getProc("glUniform2fv");
	glUniform2iv = (PFNGLUNIFORM2IVPROC)getProc("glUniform2iv");
	glUniform3fv = (PFNGLUNIFORM3FVPROC)getProc("glUniform3fv");
	glUniform3iv = (PFNGLUNIFORM3IVPROC)getProc("glUniform3iv");
	glUniform4fv = (PFNGLUNIFORM4FVPROC)getProc("glUniform4fv");
	glUniform4iv = (PFNGLUNIFORM4IVPROC)getProc("glUniform4iv");
	glUniformMatrix2fv = (PFNGLUNIFORMMATRIX2FVPROC)getProc("glUniformMatrix2fv");
	glUniformMatrix3fv = (PFNGLUNIFORMMATRIX3FVPROC)getProc("glUniformMatrix3fv");
	glUniformMatrix4fv = (PFNGLUNIFORMMATRIX4FVPROC)getProc("glUniformMatrix4fv");
}

DllExport(Fragment*) vmCreate()
{
	Fragment* ptr = new Fragment();
	ptr->Instructions = std::vector<std::vector<Instruction>>();
	ptr->Next = nullptr;
	return ptr;
}

DllExport(void) vmDelete(Fragment* frag)
{
	frag->Instructions.clear();
	frag->Next = nullptr;
	delete frag;
}

DllExport(bool) vmHasNext(Fragment* frag)
{
	return frag->Next != nullptr;
}

DllExport(Fragment*) vmGetNext(Fragment* frag)
{
	return frag->Next;
}


DllExport(void) vmLink(Fragment* left, Fragment* right)
{
	left->Next = right;
}

DllExport(void) vmUnlink(Fragment* left)
{
	left->Next = nullptr;
}


DllExport(int) vmNewBlock(Fragment* frag)
{
	int s = (int)frag->Instructions.size();
	frag->Instructions.push_back(std::vector<Instruction>());
	return s;
}

DllExport(void) vmClearBlock(Fragment* frag, int block)
{
	frag->Instructions[block].clear();
}

DllExport(void) vmAppend1(Fragment* frag, int block, InstructionCode code, intptr_t arg0)
{
	frag->Instructions[block].push_back({ code, arg0, 0, 0, 0, 0 });
}

DllExport(void) vmAppend2(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1)
{
	frag->Instructions[block].push_back({ code, arg0, arg1, 0, 0, 0 });
}

DllExport(void) vmAppend3(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1, intptr_t arg2)
{
	frag->Instructions[block].push_back({ code, arg0, arg1, arg2, 0, 0 });
}

DllExport(void) vmAppend4(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3)
{
	frag->Instructions[block].push_back({ code, arg0, arg1, arg2, arg3, 0 });
}

DllExport(void) vmAppend5(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3, intptr_t arg4)
{
	frag->Instructions[block].push_back({ code, arg0, arg1, arg2, arg3, arg4 });
}

DllExport(void) vmClear(Fragment* frag)
{
	frag->Instructions.clear();
}

void runInstruction(Instruction* i)
{
	switch (i->Code)
	{
	case BindVertexArray:
		glBindVertexArray((GLuint)i->Arg0);
		break;
	case BindProgram:
		glUseProgram((GLuint)i->Arg0);
		break;
	case ActiveTexture:
		glActiveTexture((GLenum)i->Arg0);
		break;
	case BindSampler:
		glBindSampler((GLuint)i->Arg0, (GLuint)i->Arg1);
		break;
	case BindTexture:
		glBindTexture((GLenum)i->Arg0, (GLuint)i->Arg1);
		break;
	case BindBufferBase:
		glBindBufferBase((GLenum)i->Arg0, (GLuint)i->Arg1, (GLuint)i->Arg2);
		break;
	case BindBufferRange:
		glBindBufferRange((GLenum)i->Arg0, (GLuint)i->Arg1, (GLuint)i->Arg2, (GLuint)i->Arg3, (GLsizeiptr)i->Arg4);
		break;
	case BindFramebuffer:
		glBindFramebuffer((GLenum)i->Arg0, (GLuint)i->Arg1);
		break;
	case Viewport:
		glViewport((GLint)i->Arg0, (GLint)i->Arg1, (GLint)i->Arg2, (GLint)i->Arg3);
		break;
	case Enable:
		glEnable((GLenum)i->Arg0);
		break;
	case Disable:
		glDisable((GLenum)i->Arg0);
		break;
	case DepthFunc:
		glDepthFunc((GLenum)i->Arg0);
		break;
	case CullFace:
		glCullFace((GLenum)i->Arg0);
		break;
	case BlendFuncSeparate:
		glBlendFuncSeparate((GLenum)i->Arg0, (GLenum)i->Arg1, (GLenum)i->Arg2, (GLenum)i->Arg3);
		break;
	case BlendEquationSeparate:
		glBlendEquationSeparate((GLenum)i->Arg0, (GLenum)i->Arg1);
		break;
	case BlendColor:
		glBlendColor((GLfloat)i->Arg0, (GLfloat)i->Arg1, (GLfloat)i->Arg2, (GLfloat)i->Arg3);
		break;
	case PolygonMode:
		glPolygonMode((GLenum)i->Arg0, (GLenum)i->Arg1);
		break;
	case StencilFuncSeparate:
		glStencilFuncSeparate((GLenum)i->Arg0, (GLenum)i->Arg1, (GLint)i->Arg2, (GLuint)i->Arg3);
		break;
	case StencilOpSeparate:
		glStencilOpSeparate((GLenum)i->Arg0, (GLenum)i->Arg1, (GLenum)i->Arg2, (GLenum)i->Arg3);
		break;
	case PatchParameter:
		glPatchParameteri((GLenum)i->Arg0, (GLint)i->Arg1);
		break;
	case DrawElements:
		glDrawElements((GLenum)i->Arg0, (GLsizei)i->Arg1, (GLenum)i->Arg2, (GLvoid*)i->Arg3);
		break;
	case DrawArrays:
		glDrawArrays((GLenum)i->Arg0, (GLint)i->Arg1, (GLsizei)i->Arg2);
		break;
	case DrawElementsInstanced:
		glDrawElementsInstanced((GLenum)i->Arg0, (GLsizei)i->Arg1, (GLenum)i->Arg2, (GLvoid*)i->Arg3, (GLuint)i->Arg4);
		break;
	case DrawArraysInstanced:
		glDrawArraysInstanced((GLenum)i->Arg0, (GLint)i->Arg1, (GLsizei)i->Arg2, (GLuint)i->Arg3);
		break;
	case Clear:
		glClear((GLbitfield)i->Arg0);
		break;
	case VertexAttribPointer:
		glVertexAttribPointer((GLuint)i->Arg0, (GLint)i->Arg1, (GLenum)i->Arg2, (GLboolean)i->Arg3, (GLsizei)i->Arg4, nullptr);
		break;
	case Uniform1fv:
		glUniform1fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
		break;
	case Uniform2fv:
		glUniform2fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
		break;
	case Uniform3fv:
		glUniform3fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
		break;
	case Uniform4fv:
		glUniform4fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
		break;
	case Uniform1iv:
		glUniform1iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
		break;
	case Uniform2iv:
		glUniform2iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
		break;
	case Uniform3iv:
		glUniform3iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
		break;
	case Uniform4iv:
		glUniform4iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
		break;
	case UniformMatrix2fv:
		glUniformMatrix2fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
		break;
	case UniformMatrix3fv:
		glUniformMatrix3fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
		break;
	case UniformMatrix4fv:
		glUniformMatrix4fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
		break;
	default:
		printf("unknown instruction code: %d\n", i->Code);
		break;
	}
}

Statistics runNoRedundancyChecks(Fragment* frag)
{
	int total = 0;
	Fragment* current = frag;
	while (current != nullptr)
	{
		for (auto itb = current->Instructions.begin(); itb != current->Instructions.end(); ++itb)
		{
			for (auto it = itb->begin(); it != itb->end(); ++it)
			{
				runInstruction(&(*it));
				total++;
			}
		}
		current = current->Next;
	}

	return { total, 0 };
}

Statistics runRedundancyChecks(Fragment* frag)
{
	State state;
	int totalInstructions = 0;

	Fragment* current = frag;
	while (current != nullptr)
	{
		for (auto itb = current->Instructions.begin(); itb != current->Instructions.end(); ++itb)
		{
			for (auto it = itb->begin(); it != itb->end(); ++it)
			{
				totalInstructions++;
				Instruction* i = &(*it);

				intptr_t arg0 = i->Arg0;

				switch (i->Code)
				{
				case BindVertexArray:
					if (state.ShouldSetVertexArray(arg0))
					{
						glBindVertexArray((GLuint)arg0);
					}
					break;
				case BindProgram:
					if (state.ShouldSetProgram(arg0))
					{
						glUseProgram((GLuint)arg0);
					}
					break;
				case ActiveTexture:
					if (state.ShouldSetActiveTexture(arg0))
					{
						glActiveTexture((GLenum)arg0);
					}
					break;
				case BindSampler:
					if (state.ShouldSetSampler((int)arg0, i->Arg1))
					{
						glBindSampler((GLuint)arg0, (GLuint)i->Arg1);
					}
					break;
				case BindTexture:
					if (state.ShouldSetTexture((GLenum)arg0, i->Arg1))
					{
						glBindTexture((GLenum)arg0, (GLuint)i->Arg1);
					}
					break;
				case BindBufferBase:
					if (state.ShouldSetBuffer((GLenum)arg0, (int)i->Arg1, i->Arg2))
					{
						glBindBufferBase((GLenum)arg0, (GLuint)i->Arg1, (GLuint)i->Arg2);
					}
					break;
				case BindBufferRange:
					if (state.ShouldSetBuffer((GLenum)arg0, (int)i->Arg1, i->Arg2))
					{
						glBindBufferRange((GLenum)arg0, (GLuint)i->Arg1, (GLuint)i->Arg2, (GLuint)i->Arg3, (GLsizeiptr)i->Arg4);
					}
					break;
				case Enable:
					if (state.ShouldEnable(arg0))
					{
						glEnable((GLenum)arg0);
					}
					break;
				case Disable:
					if (state.ShouldDisable(arg0))
					{
						glDisable((GLenum)arg0);
					}
					break;
				case DepthFunc:
					if (state.ShouldSetDepthFunc(arg0))
					{
						glDepthFunc((GLenum)arg0);
					}
					break;
				case CullFace:
					if (state.ShouldSetCullFace(arg0))
					{
						glCullFace((GLenum)arg0);
					}
					break;
				case BlendFuncSeparate:
					if (state.ShouldSetBlendFunc(arg0, i->Arg1, i->Arg2, i->Arg3))
					{
						glBlendFuncSeparate((GLenum)arg0, (GLenum)i->Arg1, (GLenum)i->Arg2, (GLenum)i->Arg3);
					}
					break;
				case BlendEquationSeparate:
					if (state.ShouldSetBlendEquation(arg0, i->Arg1))
					{
						glBlendEquationSeparate((GLenum)arg0, (GLenum)i->Arg1);
					}
					break;
				case BlendColor:
					if (state.ShouldSetBlendColor(arg0, i->Arg1, i->Arg2, i->Arg3))
					{
						glBlendColor((GLfloat)arg0, (GLfloat)i->Arg1, (GLfloat)i->Arg2, (GLfloat)i->Arg3);
					}
					break;
				case PolygonMode:
					if (state.ShouldSetPolygonMode(arg0, i->Arg1))
					{
						glPolygonMode((GLenum)arg0, (GLenum)i->Arg1);
					}
					break;
				case StencilFuncSeparate:
					if (state.ShouldSetStencilFunc(arg0, i->Arg1, i->Arg2, i->Arg3))
					{
						glStencilFuncSeparate((GLenum)arg0, (GLenum)i->Arg1, (GLint)i->Arg2, (GLuint)i->Arg3);
					}
					break;
				case StencilOpSeparate:
					if (state.ShouldSetStencilOp(arg0, i->Arg1, i->Arg2, i->Arg3))
					{
						glStencilOpSeparate((GLenum)arg0, (GLenum)i->Arg1, (GLenum)i->Arg2, (GLenum)i->Arg3);
					}
					break;
				case PatchParameter:
					if (state.ShouldSetPatchParameter(arg0, i->Arg1))
					{
						glPatchParameteri((GLenum)arg0, (GLint)i->Arg1);
					}
					break;




				case BindFramebuffer:
					glBindFramebuffer((GLenum)i->Arg0, (GLuint)i->Arg1);
					break;
				case Viewport:
					glViewport((GLint)i->Arg0, (GLint)i->Arg1, (GLint)i->Arg2, (GLint)i->Arg3);
					break;
				case DrawElements:
					glDrawElements((GLenum)i->Arg0, (GLsizei)i->Arg1, (GLenum)i->Arg2, (GLvoid*)i->Arg3);
					break;
				case DrawArrays:
					glDrawArrays((GLenum)i->Arg0, (GLint)i->Arg1, (GLsizei)i->Arg2);
					break;
				case DrawElementsInstanced:
					glDrawElementsInstanced((GLenum)i->Arg0, (GLsizei)i->Arg1, (GLenum)i->Arg2, (GLvoid*)i->Arg3, (GLuint)i->Arg4);
					break;
				case DrawArraysInstanced:
					glDrawArraysInstanced((GLenum)i->Arg0, (GLint)i->Arg1, (GLsizei)i->Arg2, (GLuint)i->Arg3);
					break;
				case Clear:
					glClear((GLbitfield)i->Arg0);
					break;
				case VertexAttribPointer:
					glVertexAttribPointer((GLuint)i->Arg0, (GLint)i->Arg1, (GLenum)i->Arg2, (GLboolean)i->Arg3, (GLsizei)i->Arg4, nullptr);
					break;
				case Uniform1fv:
					glUniform1fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
					break;
				case Uniform2fv:
					glUniform2fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
					break;
				case Uniform3fv:
					glUniform3fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
					break;
				case Uniform4fv:
					glUniform4fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
					break;
				case Uniform1iv:
					glUniform1iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
					break;
				case Uniform2iv:
					glUniform2iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
					break;
				case Uniform3iv:
					glUniform3iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
					break;
				case Uniform4iv:
					glUniform4iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
					break;
				case UniformMatrix2fv:
					glUniformMatrix2fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
					break;
				case UniformMatrix3fv:
					glUniformMatrix3fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
					break;
				case UniformMatrix4fv:
					glUniformMatrix4fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
					break;
				default:
					printf("unknown instruction code: %d\n", i->Code);
					break;
				}

			}
		}
		current = current->Next;
	}

	int rem = state.GetRemovedInstructions();
	state.Reset();
	return { totalInstructions, rem };
}


DllExport(void) vmRunSingle(Fragment* frag)
{
	if (!initialized)
	{
		printf("vm not initialized\n");
		return;
	}

	for (auto itb = frag->Instructions.begin(); itb != frag->Instructions.end(); ++itb)
	{
		for (auto it = itb->begin(); it != itb->end(); ++it)
		{
			runInstruction(&(*it));
		}
	}
}

DllExport(void) vmRun(Fragment* frag, VMMode mode, Statistics& stats)
{
	//DebugBreak();
	if (!initialized)
	{
		printf("vm not initialized\n");
		return;
	}

	if ((mode & RuntimeRedundancyChecks) != 0)
	{
		auto s = runRedundancyChecks(frag);
		stats = s;
	}
	else
	{
		auto s = runNoRedundancyChecks(frag);
		stats = s;
	}
}







