PROTO_DIR      := ../proto
PROTO_SRC      := $(wildcard $(PROTO_DIR)/*.proto)
SERVER_OUT     := src/GameServer/Packets/Generated
CLIENT_OUT     := ../Client/Assets/Scripts/Generated
PROTOBUF_VER   := 3.29.3

.PHONY: proto proto-deps clean-proto

## make proto — .proto → Server C# + Client C# 동시 생성
proto: $(PROTO_SRC)
	@mkdir -p $(SERVER_OUT) $(CLIENT_OUT)
	protoc --proto_path=$(PROTO_DIR) --csharp_out=$(SERVER_OUT) $(PROTO_SRC)
	protoc --proto_path=$(PROTO_DIR) --csharp_out=$(CLIENT_OUT) $(PROTO_SRC)
	@echo "✅  Server: $(SERVER_OUT)/"
	@echo "✅  Client: $(CLIENT_OUT)/"

## make proto-deps — Unity용 Google.Protobuf.dll 다운로드 (최초 1회)
proto-deps:
	@mkdir -p ../Client/Assets/Plugins
	curl -sL "https://www.nuget.org/api/v2/package/Google.Protobuf/$(PROTOBUF_VER)" -o /tmp/protobuf.nupkg
	cd /tmp && unzip -q -o protobuf.nupkg "lib/netstandard2.1/Google.Protobuf.dll"
	cp /tmp/lib/netstandard2.1/Google.Protobuf.dll ../Client/Assets/Plugins/
	@echo "✅  Google.Protobuf.dll → Client/Assets/Plugins/"

## make clean-proto — 생성된 C# 파일 삭제
clean-proto:
	rm -f $(SERVER_OUT)/*.cs $(CLIENT_OUT)/*.cs
	@echo "🗑️   Cleaned generated files"
