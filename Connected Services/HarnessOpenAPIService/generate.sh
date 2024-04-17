
# https://github.com/RicoSuter/NSwag/wiki/NSwag-Configuration-Document
# https://github.com/RicoSuter/NSwag/wiki/CSharpClientGeneratorSettings

# see https://github.com/RicoSuter/NSwag/issues/850

# note it is not clear how to completely remove "metrics" class from the generated code. "excludedTypeNames" only works on DTOs. So you may need to manually modify client-v1.yaml

npx nswag@13.20.0 run HarnessOpenAPIS.nswag /variables:
