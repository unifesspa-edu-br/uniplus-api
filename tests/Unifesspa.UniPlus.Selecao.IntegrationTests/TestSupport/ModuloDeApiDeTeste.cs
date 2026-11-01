using Unifesspa.UniPlus.Infrastructure.Core.Routing;

// O host de teste monta a rota de todo controller pelo módulo do assembly; o controller de teste da consulta
// administrativa vive aqui e responde sob o prefixo da Seleção.
[assembly: ApiModule("selecao")]
