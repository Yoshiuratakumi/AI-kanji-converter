#include "Dictionary.h"

#include <algorithm>
#include <cstdint>
#include <fstream>
#include <sstream>
#include <windows.h>

// ── Encoding helpers ──────────────────────────────────────────────────────────

/*static*/ std::string Dictionary::WideToUtf8(const std::wstring& w)
{
    if (w.empty()) return {};
    int n = WideCharToMultiByte(CP_UTF8, 0, w.data(), static_cast<int>(w.size()),
                                nullptr, 0, nullptr, nullptr);
    std::string s(n, '\0');
    WideCharToMultiByte(CP_UTF8, 0, w.data(), static_cast<int>(w.size()),
                        s.data(), n, nullptr, nullptr);
    return s;
}

/*static*/ std::wstring Dictionary::Utf8ToWide(const std::string& s)
{
    if (s.empty()) return {};
    int n = MultiByteToWideChar(CP_UTF8, 0, s.data(), static_cast<int>(s.size()),
                                nullptr, 0);
    std::wstring w(n, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, s.data(), static_cast<int>(s.size()),
                        w.data(), n);
    return w;
}

// ── Loading ───────────────────────────────────────────────────────────────────

bool Dictionary::Load(const std::wstring& dictPath)
{
    // Use _wfopen so non-ANSI paths work on MinGW.
    FILE* fp = _wfopen(dictPath.c_str(), L"rb");
    if (!fp) return false;

    fseek(fp, 0, SEEK_END);
    long fileSize = ftell(fp);
    fseek(fp, 0, SEEK_SET);
    std::string content(static_cast<size_t>(fileSize), '\0');
    fread(&content[0], 1, static_cast<size_t>(fileSize), fp);
    fclose(fp);

    dict_.clear();
    std::istringstream fStream(content);
    std::string line;

    while (std::getline(fStream, line)) {
        // Strip UTF-8 BOM on first line
        if (!line.empty() &&
            static_cast<uint8_t>(line[0]) == 0xEF &&
            static_cast<uint8_t>(line[1]) == 0xBB &&
            static_cast<uint8_t>(line[2]) == 0xBF)
            line.erase(0, 3);

        if (!line.empty() && line.back() == '\r') line.pop_back();
        if (line.empty() || line[0] == ';') continue;  // SKK comment

        // Split "ひらがな /候補1/候補2/..."
        auto spacePos = line.find(' ');
        if (spacePos == std::string::npos) continue;

        std::string keyUtf8 = line.substr(0, spacePos);
        std::string rest     = line.substr(spacePos + 1);

        std::wstring key = Utf8ToWide(keyUtf8);
        std::vector<std::wstring> candidates;

        std::istringstream ss(rest);
        std::string token;
        while (std::getline(ss, token, '/')) {
            if (token.empty()) continue;
            // Strip SKK annotation: "漢字;注記" → "漢字"
            auto semi = token.find(';');
            if (semi != std::string::npos)
                token = token.substr(0, semi);
            if (!token.empty())
                candidates.push_back(Utf8ToWide(token));
        }

        if (!candidates.empty())
            dict_[key] = std::move(candidates);
    }

    return !dict_.empty();
}

// ── Lookup ────────────────────────────────────────────────────────────────────

std::vector<std::wstring> Dictionary::Lookup(const std::wstring& hiragana,
                                              int maxCount) const
{
    auto it = dict_.find(hiragana);
    if (it == dict_.end()) return {};

    const auto& all = it->second;
    int count = std::min(static_cast<int>(all.size()), maxCount);
    return {all.begin(), all.begin() + count};
}
