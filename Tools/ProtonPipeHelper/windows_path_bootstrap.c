#define UNICODE
#define _UNICODE

#include <windows.h>
#include <tlhelp32.h>
#include <wchar.h>
#include <stdlib.h>

#define MAX_WINDOW_POLICY_PROCESSES 2048
#define MIN_GAME_CLIENT_WIDTH 320
#define MIN_GAME_CLIENT_HEIGHT 200

typedef struct window_policy_context
{
    DWORD root_process_id;
    HANDLE stop_event;
    HWND bootstrap_window;
    BOOL center_windows;
    BOOL hide_window_menu;
} window_policy_context;

typedef struct window_policy_pass
{
    DWORD process_ids[MAX_WINDOW_POLICY_PROCESSES];
    size_t process_count;
    HWND bootstrap_window;
    BOOL center_windows;
    BOOL hide_window_menu;
} window_policy_pass;

static BOOL environment_flag_enabled(const wchar_t* name)
{
    wchar_t value[8] = { 0 };
    DWORD length = GetEnvironmentVariableW(name, value, ARRAYSIZE(value));
    return length > 0 && length < ARRAYSIZE(value) &&
        (value[0] == L'1' || value[0] == L'y' || value[0] == L'Y' ||
         value[0] == L't' || value[0] == L'T');
}

static BOOL process_list_contains(
    const window_policy_pass* pass,
    DWORD process_id)
{
    size_t index;
    for (index = 0; index < pass->process_count; ++index)
    {
        if (pass->process_ids[index] == process_id)
            return TRUE;
    }
    return FALSE;
}

static void collect_process_tree(
    window_policy_pass* pass,
    DWORD root_process_id)
{
    HANDLE snapshot;
    PROCESSENTRY32W entry;
    BOOL added;

    pass->process_count = 0;
    pass->process_ids[pass->process_count++] = root_process_id;
    snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE)
        return;

    do
    {
        added = FALSE;
        entry.dwSize = sizeof(entry);
        if (Process32FirstW(snapshot, &entry))
        {
            do
            {
                if (pass->process_count >= MAX_WINDOW_POLICY_PROCESSES)
                    break;
                if (!process_list_contains(pass, entry.th32ProcessID) &&
                    process_list_contains(pass, entry.th32ParentProcessID))
                {
                    pass->process_ids[pass->process_count++] = entry.th32ProcessID;
                    added = TRUE;
                }
            } while (Process32NextW(snapshot, &entry));
        }
    } while (added && pass->process_count < MAX_WINDOW_POLICY_PROCESSES);

    CloseHandle(snapshot);
}

static BOOL CALLBACK apply_window_policy(HWND window, LPARAM parameter)
{
    window_policy_pass* pass = (window_policy_pass*)parameter;
    DWORD process_id = 0;
    LONG_PTR style;
    LONG_PTR extended_style;
    LONG_PTR borderless_style;
    LONG_PTR borderless_extended_style;
    RECT client_rectangle;
    RECT window_rectangle;
    int client_width;
    int client_height;
    int target_x;
    int target_y;
    int screen_width;
    int screen_height;
    BOOL process_in_tree;
    BOOL style_changed;
    BOOL menu_changed = FALSE;

    GetWindowThreadProcessId(window, &process_id);
    process_in_tree = window == pass->bootstrap_window ||
        process_list_contains(pass, process_id);
    // Never mutate orphaned Wine windows. Battle Gear 4 re-parents game.exe,
    // but cross-process SetMenu/FRAMECHANGED during its startup tears down the
    // guest. Cosmetic menu hiding must remain subordinate to game stability.
    if (!process_in_tree)
        return TRUE;
    if (!IsWindowVisible(window) || !GetClientRect(window, &client_rectangle) ||
        !GetWindowRect(window, &window_rectangle))
        return TRUE;

    client_width = client_rectangle.right - client_rectangle.left;
    client_height = client_rectangle.bottom - client_rectangle.top;
    if (client_width < MIN_GAME_CLIENT_WIDTH ||
        client_height < MIN_GAME_CLIENT_HEIGHT)
        return TRUE;

    style = GetWindowLongPtrW(window, GWL_STYLE);
    if ((style & WS_CHILD) != 0)
        return TRUE;
    extended_style = GetWindowLongPtrW(window, GWL_EXSTYLE);
    borderless_style = style &
        ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX |
          WS_MAXIMIZEBOX | WS_SYSMENU);
    borderless_extended_style = extended_style &
        ~(WS_EX_DLGMODALFRAME | WS_EX_CLIENTEDGE |
          WS_EX_STATICEDGE | WS_EX_WINDOWEDGE);
    if (pass->hide_window_menu && GetMenu(window) != NULL)
        menu_changed = SetMenu(window, NULL);
    style_changed = borderless_style != style ||
        borderless_extended_style != extended_style || menu_changed;

    if (style_changed)
    {
        SetWindowLongPtrW(window, GWL_STYLE, borderless_style);
        SetWindowLongPtrW(window, GWL_EXSTYLE, borderless_extended_style);
    }

    target_x = window_rectangle.left;
    target_y = window_rectangle.top;
    if (pass->center_windows)
    {
        screen_width = GetSystemMetrics(SM_CXSCREEN);
        screen_height = GetSystemMetrics(SM_CYSCREEN);
        target_x = max(0, (screen_width - client_width) / 2);
        target_y = max(0, (screen_height - client_height) / 2);
    }

    if (style_changed)
    {
        // Preserve the game's client resolution. With the non-client frame
        // gone, the outer dimensions are exactly the original client size.
        SetWindowPos(
            window,
            NULL,
            target_x,
            target_y,
            client_width,
            client_height,
            SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOZORDER | SWP_FRAMECHANGED);
    }
    else if (pass->center_windows &&
             (window_rectangle.left != target_x ||
              window_rectangle.top != target_y))
    {
        SetWindowPos(
            window,
            NULL,
            target_x,
            target_y,
            0,
            0,
            SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOZORDER | SWP_NOSIZE);
    }
    return TRUE;
}

static DWORD WINAPI window_policy_thread(LPVOID parameter)
{
    window_policy_context* context = (window_policy_context*)parameter;
    window_policy_pass pass;
    DWORD wait_result;

    pass.bootstrap_window = context->bootstrap_window;
    pass.center_windows = context->center_windows;
    pass.hide_window_menu = context->hide_window_menu;
    do
    {
        collect_process_tree(&pass, context->root_process_id);
        EnumWindows(apply_window_policy, (LPARAM)&pass);
        wait_result = WaitForSingleObject(context->stop_event, 250);
    } while (wait_result == WAIT_TIMEOUT);
    return 0;
}

static LRESULT CALLBACK self_test_window_proc(
    HWND window,
    UINT message,
    WPARAM wparam,
    LPARAM lparam)
{
    return DefWindowProcW(window, message, wparam, lparam);
}

static int run_borderless_self_test(void)
{
    static const wchar_t class_name[] = L"TeknoParrotBorderlessSelfTest";
    const int expected_width = 640;
    const int expected_height = 480;
    WNDCLASSW window_class = { 0 };
    RECT requested_rectangle = { 0, 0, expected_width, expected_height };
    RECT actual_rectangle;
    window_policy_context context = { 0 };
    HANDLE thread_handle = NULL;
    HWND window = NULL;
    MSG message;
    DWORD test_start;
    LONG_PTR style;
    LONG_PTR extended_style;
    int expected_x;
    int expected_y;
    int result = ERROR_GEN_FAILURE;

    window_class.lpfnWndProc = self_test_window_proc;
    window_class.hInstance = GetModuleHandleW(NULL);
    window_class.lpszClassName = class_name;
    if (!RegisterClassW(&window_class) &&
        GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
        return (int)GetLastError();

    AdjustWindowRectEx(
        &requested_rectangle,
        WS_OVERLAPPEDWINDOW,
        FALSE,
        WS_EX_CLIENTEDGE);
    window = CreateWindowExW(
        WS_EX_CLIENTEDGE,
        class_name,
        L"TeknoParrot borderless self-test",
        WS_OVERLAPPEDWINDOW,
        13,
        17,
        requested_rectangle.right - requested_rectangle.left,
        requested_rectangle.bottom - requested_rectangle.top,
        NULL,
        NULL,
        window_class.hInstance,
        NULL);
    if (window == NULL)
    {
        result = (int)GetLastError();
        goto cleanup;
    }
    ShowWindow(window, SW_SHOWNOACTIVATE);
    UpdateWindow(window);
    context.root_process_id = GetCurrentProcessId();
    context.bootstrap_window = window;
    context.center_windows = TRUE;
    context.stop_event = CreateEventW(NULL, TRUE, FALSE, NULL);
    if (context.stop_event == NULL)
    {
        result = (int)GetLastError();
        goto cleanup;
    }
    thread_handle = CreateThread(
        NULL,
        0,
        window_policy_thread,
        &context,
        0,
        NULL);
    if (thread_handle == NULL)
    {
        result = (int)GetLastError();
        goto cleanup;
    }
    // Cross-thread non-client changes can synchronously notify the owning UI
    // thread. Pump it here just as a real game does in its message loop.
    test_start = GetTickCount();
    while (GetTickCount() - test_start < 750)
    {
        while (PeekMessageW(&message, NULL, 0, 0, PM_REMOVE))
        {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        Sleep(10);
    }

    style = GetWindowLongPtrW(window, GWL_STYLE);
    extended_style = GetWindowLongPtrW(window, GWL_EXSTYLE);
    if ((style & (WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX |
                  WS_MAXIMIZEBOX | WS_SYSMENU)) != 0 ||
        (extended_style & (WS_EX_DLGMODALFRAME | WS_EX_CLIENTEDGE |
                           WS_EX_STATICEDGE | WS_EX_WINDOWEDGE)) != 0)
    {
        result = 1;
        goto cleanup;
    }
    if (!GetWindowRect(window, &actual_rectangle) ||
        actual_rectangle.right - actual_rectangle.left != expected_width ||
        actual_rectangle.bottom - actual_rectangle.top != expected_height)
    {
        result = 2;
        goto cleanup;
    }
    expected_x = max(0, (GetSystemMetrics(SM_CXSCREEN) - expected_width) / 2);
    expected_y = max(0, (GetSystemMetrics(SM_CYSCREEN) - expected_height) / 2);
    if (actual_rectangle.left != expected_x || actual_rectangle.top != expected_y)
    {
        result = 3;
        goto cleanup;
    }
    result = 0;

cleanup:
    if (context.stop_event != NULL)
        SetEvent(context.stop_event);
    if (thread_handle != NULL)
    {
        WaitForSingleObject(thread_handle, 2000);
        CloseHandle(thread_handle);
    }
    if (context.stop_event != NULL)
        CloseHandle(context.stop_event);
    if (window != NULL)
        DestroyWindow(window);
    UnregisterClassW(class_name, window_class.hInstance);
    return result;
}

static int append_quoted_argument(
    wchar_t* destination,
    size_t capacity,
    size_t* length,
    const wchar_t* value)
{
    size_t backslashes = 0;
    const wchar_t* cursor;

    if (*length + 1 >= capacity)
        return 0;
    destination[(*length)++] = L'"';

    for (cursor = value; ; ++cursor)
    {
        wchar_t character = *cursor;
        if (character == L'\\')
        {
            ++backslashes;
            continue;
        }

        if (character == L'"' || character == L'\0')
        {
            size_t copies = backslashes * 2 + (character == L'"' ? 1 : 0);
            while (copies-- > 0)
            {
                if (*length + 1 >= capacity)
                    return 0;
                destination[(*length)++] = L'\\';
            }
        }
        else
        {
            while (backslashes-- > 0)
            {
                if (*length + 1 >= capacity)
                    return 0;
                destination[(*length)++] = L'\\';
            }
        }
        backslashes = 0;

        if (character == L'\0')
            break;
        if (*length + 1 >= capacity)
            return 0;
        destination[(*length)++] = character;
    }

    if (*length + 1 >= capacity)
        return 0;
    destination[(*length)++] = L'"';
    destination[*length] = L'\0';
    return 1;
}

static wchar_t* build_command_line(int argc, wchar_t** argv)
{
    size_t capacity = 1;
    size_t length = 0;
    wchar_t* result;
    int index;

    for (index = 2; index < argc; ++index)
    {
        size_t argument_length = wcslen(argv[index]);
        if (argument_length > 32767 || capacity > 32767 - (argument_length * 2 + 4))
            return NULL;
        capacity += argument_length * 2 + 4;
    }

    result = (wchar_t*)calloc(capacity, sizeof(wchar_t));
    if (result == NULL)
        return NULL;

    for (index = 2; index < argc; ++index)
    {
        if (index > 2)
            result[length++] = L' ';
        if (!append_quoted_argument(result, capacity, &length, argv[index]))
        {
            free(result);
            return NULL;
        }
    }
    return result;
}

static wchar_t* build_prelaunch_command_line(
    const wchar_t* loader,
    const wchar_t* core,
    const wchar_t* helper)
{
    const wchar_t* values[3] = { loader, core, helper };
    size_t capacity = 1;
    size_t length = 0;
    wchar_t* result;
    int index;

    for (index = 0; index < 3; ++index)
    {
        size_t argument_length = wcslen(values[index]);
        if (argument_length > 32767 || capacity > 32767 - (argument_length * 2 + 4))
            return NULL;
        capacity += argument_length * 2 + 4;
    }

    result = (wchar_t*)calloc(capacity, sizeof(wchar_t));
    if (result == NULL)
        return NULL;

    for (index = 0; index < 3; ++index)
    {
        if (index > 0)
            result[length++] = L' ';
        if (!append_quoted_argument(result, capacity, &length, values[index]))
        {
            free(result);
            return NULL;
        }
    }
    return result;
}

static wchar_t* build_direct_prelaunch_command_line(
    const wchar_t* executable,
    const wchar_t* arguments)
{
    size_t argument_length = wcslen(executable);
    size_t extra_length = arguments == NULL ? 0 : wcslen(arguments);
    size_t capacity;
    size_t length = 0;
    wchar_t* result;

    if (argument_length > 32767)
        return NULL;
    if (extra_length > 32767 || argument_length * 2 + extra_length + 6 > 32767)
        return NULL;
    capacity = argument_length * 2 + extra_length + 6;
    result = (wchar_t*)calloc(capacity, sizeof(wchar_t));
    if (result == NULL)
        return NULL;
    if (!append_quoted_argument(result, capacity, &length, executable))
    {
        free(result);
        return NULL;
    }
    if (extra_length > 0)
    {
        result[length++] = L' ';
        wcscpy_s(result + length, capacity - length, arguments);
    }
    return result;
}

int wmain(int argc, wchar_t** argv)
{
    DWORD attributes;
    DWORD previous_path_length;
    size_t library_length;
    size_t combined_length;
    size_t game_combined_length;
    wchar_t* previous_path = NULL;
    wchar_t* combined_path = NULL;
    wchar_t* game_combined_path = NULL;
    wchar_t* command_line = NULL;
    wchar_t* prelaunch_executable = NULL;
    wchar_t* prelaunch_arguments = NULL;
    wchar_t* prelaunch_command_line = NULL;
    wchar_t* prelaunch_working_directory = NULL;
    wchar_t* prelaunch_ready_pipe = NULL;
    wchar_t* game_working_directory = NULL;
    wchar_t* game_core_path = NULL;
    const wchar_t* prelaunch_application = NULL;
    const wchar_t* prelaunch_current_directory = NULL;
    STARTUPINFOW startup_info = { 0 };
    PROCESS_INFORMATION process_info = { 0 };
    PROCESS_INFORMATION prelaunch_process_info = { 0 };
    window_policy_context window_policy = { 0 };
    HANDLE window_policy_thread_handle = NULL;
    DWORD exit_code = ERROR_GEN_FAILURE;
    DWORD prelaunch_length;
    DWORD prelaunch_arguments_length;
    DWORD prelaunch_working_length;
    DWORD prelaunch_ready_pipe_length;
    DWORD prelaunch_creation_flags = 0;
    DWORD game_working_length;
    DWORD game_core_length;
    DWORD wait_result;
    int result = ERROR_INVALID_PARAMETER;

    if (argc == 2 && wcscmp(argv[1], L"--self-test-borderless") == 0)
        return run_borderless_self_test();

    if (argc < 3 || argv[1][0] == L'\0' || argv[2][0] == L'\0')
        return ERROR_INVALID_PARAMETER;

    if (environment_flag_enabled(L"TP_HIDE_LAUNCH_CONSOLE"))
    {
        HWND console_window = GetConsoleWindow();
        if (console_window != NULL)
            ShowWindow(console_window, SW_HIDE);
    }

    attributes = GetFileAttributesW(argv[1]);
    if (attributes == INVALID_FILE_ATTRIBUTES || !(attributes & FILE_ATTRIBUTE_DIRECTORY))
        return ERROR_PATH_NOT_FOUND;
    attributes = GetFileAttributesW(argv[2]);
    if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_DIRECTORY))
        return ERROR_FILE_NOT_FOUND;

    previous_path_length = GetEnvironmentVariableW(L"PATH", NULL, 0);
    if (previous_path_length > 0)
    {
        previous_path = (wchar_t*)calloc(previous_path_length, sizeof(wchar_t));
        if (previous_path == NULL)
            return ERROR_NOT_ENOUGH_MEMORY;
        if (GetEnvironmentVariableW(L"PATH", previous_path, previous_path_length) == 0)
            previous_path[0] = L'\0';
    }

    library_length = wcslen(argv[1]);
    combined_length = library_length + 1 +
        (previous_path != NULL ? wcslen(previous_path) : 0) + 1;
    if (combined_length > 32767)
    {
        result = ERROR_ENVVAR_NOT_FOUND;
        goto cleanup;
    }

    combined_path = (wchar_t*)calloc(combined_length, sizeof(wchar_t));
    if (combined_path == NULL)
    {
        result = ERROR_NOT_ENOUGH_MEMORY;
        goto cleanup;
    }
    wcscpy_s(combined_path, combined_length, argv[1]);
    if (previous_path != NULL && previous_path[0] != L'\0')
    {
        wcscat_s(combined_path, combined_length, L";");
        wcscat_s(combined_path, combined_length, previous_path);
    }
    if (!SetEnvironmentVariableW(L"PATH", combined_path))
    {
        result = (int)GetLastError();
        goto cleanup;
    }

    game_working_length = GetEnvironmentVariableW(
        L"TP_GAME_WORKING_DIRECTORY",
        NULL,
        0);
    if (game_working_length > 1)
    {
        if (argc < 4)
        {
            result = ERROR_INVALID_PARAMETER;
            goto cleanup;
        }
        game_core_length = GetFullPathNameW(argv[3], 0, NULL, NULL);
        if (game_core_length == 0 || game_core_length > 32767)
        {
            result = game_core_length == 0 ?
                (int)GetLastError() : ERROR_ENVVAR_NOT_FOUND;
            goto cleanup;
        }
        game_core_path = (wchar_t*)calloc(game_core_length, sizeof(wchar_t));
        if (game_core_path == NULL)
        {
            result = ERROR_NOT_ENOUGH_MEMORY;
            goto cleanup;
        }
        if (GetFullPathNameW(
                argv[3],
                game_core_length,
                game_core_path,
                NULL) == 0)
        {
            result = (int)GetLastError();
            goto cleanup;
        }
        argv[3] = game_core_path;

        if (game_working_length > 32767)
        {
            result = ERROR_ENVVAR_NOT_FOUND;
            goto cleanup;
        }
        game_working_directory = (wchar_t*)calloc(
            game_working_length,
            sizeof(wchar_t));
        if (game_working_directory == NULL)
        {
            result = ERROR_NOT_ENOUGH_MEMORY;
            goto cleanup;
        }
        if (GetEnvironmentVariableW(
                L"TP_GAME_WORKING_DIRECTORY",
                game_working_directory,
                game_working_length) == 0)
        {
            result = (int)GetLastError();
            goto cleanup;
        }
        attributes = GetFileAttributesW(game_working_directory);
        if (attributes == INVALID_FILE_ATTRIBUTES ||
            !(attributes & FILE_ATTRIBUTE_DIRECTORY))
        {
            result = ERROR_PATH_NOT_FOUND;
            goto cleanup;
        }
        game_combined_length = wcslen(game_working_directory) + 1 +
            wcslen(combined_path) + 1;
        if (game_combined_length > 32767)
        {
            result = ERROR_ENVVAR_NOT_FOUND;
            goto cleanup;
        }
        game_combined_path = (wchar_t*)calloc(
            game_combined_length,
            sizeof(wchar_t));
        if (game_combined_path == NULL)
        {
            result = ERROR_NOT_ENOUGH_MEMORY;
            goto cleanup;
        }
        wcscpy_s(
            game_combined_path,
            game_combined_length,
            game_working_directory);
        wcscat_s(game_combined_path, game_combined_length, L";");
        wcscat_s(game_combined_path, game_combined_length, combined_path);
        if (!SetEnvironmentVariableW(L"PATH", game_combined_path))
        {
            result = (int)GetLastError();
            goto cleanup;
        }
        /*
         * Keep the bootstrap and loader in the shared runtime directory.
         * OpenParrotLoader consumes TP_GAME_WORKING_DIRECTORY and applies it to
         * the suspended target process. Changing the bootstrap directory here
         * makes Wine resolve loader-time components from the dump and can end
         * x86 RemoteThread injection before the game creates its first window.
         */
    }

    prelaunch_length = GetEnvironmentVariableW(L"TP_PRELAUNCH_EXECUTABLE", NULL, 0);
    if (prelaunch_length > 1)
    {
        if (argc < 4)
        {
            result = ERROR_INVALID_PARAMETER;
            goto cleanup;
        }
        prelaunch_executable = (wchar_t*)calloc(prelaunch_length, sizeof(wchar_t));
        if (prelaunch_executable == NULL)
        {
            result = ERROR_NOT_ENOUGH_MEMORY;
            goto cleanup;
        }
        if (GetEnvironmentVariableW(
                L"TP_PRELAUNCH_EXECUTABLE",
                prelaunch_executable,
                prelaunch_length) == 0)
        {
            result = (int)GetLastError();
            goto cleanup;
        }
        attributes = GetFileAttributesW(prelaunch_executable);
        if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_DIRECTORY))
        {
            result = ERROR_FILE_NOT_FOUND;
            goto cleanup;
        }

        if (environment_flag_enabled(L"TP_PRELAUNCH_DIRECT"))
        {
            prelaunch_application = prelaunch_executable;
            prelaunch_arguments_length = GetEnvironmentVariableW(
                L"TP_PRELAUNCH_ARGUMENTS",
                NULL,
                0);
            if (prelaunch_arguments_length > 1)
            {
                if (prelaunch_arguments_length > 32767)
                {
                    result = ERROR_ENVVAR_NOT_FOUND;
                    goto cleanup;
                }
                prelaunch_arguments = (wchar_t*)calloc(
                    prelaunch_arguments_length,
                    sizeof(wchar_t));
                if (prelaunch_arguments == NULL)
                {
                    result = ERROR_NOT_ENOUGH_MEMORY;
                    goto cleanup;
                }
                if (GetEnvironmentVariableW(
                        L"TP_PRELAUNCH_ARGUMENTS",
                        prelaunch_arguments,
                        prelaunch_arguments_length) == 0)
                {
                    result = (int)GetLastError();
                    goto cleanup;
                }
            }
            prelaunch_command_line = build_direct_prelaunch_command_line(
                prelaunch_executable,
                prelaunch_arguments);
            prelaunch_working_length = GetEnvironmentVariableW(
                L"TP_PRELAUNCH_WORKING_DIRECTORY",
                NULL,
                0);
            if (prelaunch_working_length > 1)
            {
                if (prelaunch_working_length > 32767)
                {
                    result = ERROR_ENVVAR_NOT_FOUND;
                    goto cleanup;
                }
                prelaunch_working_directory = (wchar_t*)calloc(
                    prelaunch_working_length,
                    sizeof(wchar_t));
                if (prelaunch_working_directory == NULL)
                {
                    result = ERROR_NOT_ENOUGH_MEMORY;
                    goto cleanup;
                }
                if (GetEnvironmentVariableW(
                        L"TP_PRELAUNCH_WORKING_DIRECTORY",
                        prelaunch_working_directory,
                        prelaunch_working_length) == 0)
                {
                    result = (int)GetLastError();
                    goto cleanup;
                }
                attributes = GetFileAttributesW(prelaunch_working_directory);
                if (attributes == INVALID_FILE_ATTRIBUTES ||
                    !(attributes & FILE_ATTRIBUTE_DIRECTORY))
                {
                    result = ERROR_PATH_NOT_FOUND;
                    goto cleanup;
                }
                prelaunch_current_directory = prelaunch_working_directory;
            }
            if (environment_flag_enabled(L"TP_PRELAUNCH_HIDE_WINDOW"))
            {
                startup_info.dwFlags |= STARTF_USESHOWWINDOW;
                startup_info.wShowWindow = SW_HIDE;
                prelaunch_creation_flags |= CREATE_NO_WINDOW;
            }
        }
        else
        {
            prelaunch_application = argv[2];
            prelaunch_command_line = build_prelaunch_command_line(
                argv[2], argv[3], prelaunch_executable);
        }
        if (prelaunch_command_line == NULL)
        {
            result = ERROR_NOT_ENOUGH_MEMORY;
            goto cleanup;
        }
        startup_info.cb = sizeof(startup_info);
        if (!CreateProcessW(
                prelaunch_application,
                prelaunch_command_line,
                NULL,
                NULL,
                FALSE,
                prelaunch_creation_flags,
                NULL,
                prelaunch_current_directory,
                &startup_info,
                &prelaunch_process_info))
        {
            result = (int)GetLastError();
            goto cleanup;
        }
        prelaunch_ready_pipe_length = GetEnvironmentVariableW(
            L"TP_PRELAUNCH_READY_PIPE",
            NULL,
            0);
        if (prelaunch_ready_pipe_length > 1)
        {
            DWORD ready_start = GetTickCount();
            if (prelaunch_ready_pipe_length > 32767)
            {
                result = ERROR_ENVVAR_NOT_FOUND;
                goto cleanup;
            }
            prelaunch_ready_pipe = (wchar_t*)calloc(
                prelaunch_ready_pipe_length,
                sizeof(wchar_t));
            if (prelaunch_ready_pipe == NULL)
            {
                result = ERROR_NOT_ENOUGH_MEMORY;
                goto cleanup;
            }
            if (GetEnvironmentVariableW(
                    L"TP_PRELAUNCH_READY_PIPE",
                    prelaunch_ready_pipe,
                    prelaunch_ready_pipe_length) == 0)
            {
                result = (int)GetLastError();
                goto cleanup;
            }
            while (!WaitNamedPipeW(prelaunch_ready_pipe, 100))
            {
                if (WaitForSingleObject(prelaunch_process_info.hProcess, 0) ==
                    WAIT_OBJECT_0)
                {
                    if (!GetExitCodeProcess(
                            prelaunch_process_info.hProcess,
                            &exit_code) || exit_code == 0)
                        result = ERROR_BROKEN_PIPE;
                    else
                        result = (int)exit_code;
                    goto cleanup;
                }
                if (GetTickCount() - ready_start >= 10000)
                {
                    result = ERROR_TIMEOUT;
                    goto cleanup;
                }
                Sleep(50);
            }
        }
        else if (environment_flag_enabled(L"TP_PRELAUNCH_WAIT_FOR_LOADER"))
        {
            wait_result = WaitForSingleObject(
                prelaunch_process_info.hProcess,
                30000);
            if (wait_result != WAIT_OBJECT_0)
            {
                result = wait_result == WAIT_TIMEOUT ?
                    ERROR_TIMEOUT : (int)GetLastError();
                goto cleanup;
            }
        }
        if (prelaunch_ready_pipe_length <= 1)
            Sleep(1000);
    }

    command_line = build_command_line(argc, argv);
    if (command_line == NULL)
    {
        result = ERROR_NOT_ENOUGH_MEMORY;
        goto cleanup;
    }

    ZeroMemory(&startup_info, sizeof(startup_info));
    startup_info.cb = sizeof(startup_info);
    if (!CreateProcessW(
            argv[2],
            command_line,
            NULL,
            NULL,
            FALSE,
            0,
            NULL,
            NULL,
            &startup_info,
            &process_info))
    {
        result = (int)GetLastError();
        goto cleanup;
    }

    if (environment_flag_enabled(L"TP_BORDERLESS_WINDOW"))
    {
        window_policy.root_process_id = process_info.dwProcessId;
        window_policy.bootstrap_window = GetConsoleWindow();
        window_policy.center_windows =
            environment_flag_enabled(L"TP_CENTER_WINDOW");
        window_policy.hide_window_menu =
            environment_flag_enabled(L"TP_HIDE_WINDOW_MENU");
        window_policy.stop_event = CreateEventW(NULL, TRUE, FALSE, NULL);
        if (window_policy.stop_event != NULL)
        {
            window_policy_thread_handle = CreateThread(
                NULL,
                0,
                window_policy_thread,
                &window_policy,
                0,
                NULL);
        }
    }

    WaitForSingleObject(process_info.hProcess, INFINITE);
    if (!GetExitCodeProcess(process_info.hProcess, &exit_code))
        exit_code = GetLastError();
    result = (int)exit_code;

cleanup:
    if (window_policy.stop_event != NULL)
        SetEvent(window_policy.stop_event);
    if (window_policy_thread_handle != NULL)
    {
        WaitForSingleObject(window_policy_thread_handle, 2000);
        CloseHandle(window_policy_thread_handle);
    }
    if (window_policy.stop_event != NULL)
        CloseHandle(window_policy.stop_event);
    if (prelaunch_process_info.hProcess != NULL &&
        environment_flag_enabled(L"TP_PRELAUNCH_TERMINATE_WITH_GAME") &&
        WaitForSingleObject(prelaunch_process_info.hProcess, 0) == WAIT_TIMEOUT)
    {
        TerminateProcess(prelaunch_process_info.hProcess, 0);
        WaitForSingleObject(prelaunch_process_info.hProcess, 2000);
    }
    if (prelaunch_process_info.hThread != NULL)
        CloseHandle(prelaunch_process_info.hThread);
    if (prelaunch_process_info.hProcess != NULL)
        CloseHandle(prelaunch_process_info.hProcess);
    if (process_info.hThread != NULL)
        CloseHandle(process_info.hThread);
    if (process_info.hProcess != NULL)
        CloseHandle(process_info.hProcess);
    free(command_line);
    free(prelaunch_command_line);
    free(prelaunch_executable);
    free(prelaunch_arguments);
    free(prelaunch_working_directory);
    free(prelaunch_ready_pipe);
    free(game_working_directory);
    free(game_core_path);
    free(game_combined_path);
    free(combined_path);
    free(previous_path);
    return result;
}
