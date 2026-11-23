namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Services;

internal static class DadosBasicosDoCandidatoSeed
{
    private const string PontoResolucaoInscricao = "INSCRICAO";
    private const string FinalidadeIdentificacao = "Identificação do candidato no processo seletivo.";
    private const string FinalidadeComunicacao = "Comunicação com o candidato sobre o processo seletivo.";
    private const string FinalidadeResidencia = FatoCandidatoSeed.FinalidadeResidencia;
    private const string FinalidadeReservaDeVagas = FatoCandidatoSeed.FinalidadeReservaDeVagas;

    private static Guid SeedId(int numero) => Guid.Parse($"fa700000-0000-7000-8000-{numero:D12}");

    internal static IReadOnlyList<FatoCandidatoSeedItem> Itens { get; } =
    [
        new(SeedId(27), "NOME", "Nome", "Nome civil do candidato, como vem do login gov.br.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:NOME", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao, Formato: FormatoTexto.Livre),
        new(SeedId(28), "DESEJA_NOME_SOCIAL", "Deseja usar nome social", "Se o candidato deseja ser identificado pelo nome social no processo seletivo.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:DESEJA_NOME_SOCIAL", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao),
        new(SeedId(29), FatoCandidato.CodigoDoNomeSocial, "Nome social", "Nome pelo qual o candidato prefere ser identificado e que aparece no lugar do nome civil.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, $"CAMPO_FORMULARIO:{FatoCandidato.CodigoDoNomeSocial}", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao, Formato: FormatoTexto.NomePessoa),
        new(SeedId(30), "CPF", "CPF", "Número do CPF do candidato.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:CPF", ClassificacaoDeProtecao.Identificador, FinalidadeIdentificacao, Formato: FormatoTexto.Cpf),
        new(SeedId(31), "RG_NUMERO", "Número do RG", "Número do RG do candidato.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:RG_NUMERO", ClassificacaoDeProtecao.Identificador, FinalidadeIdentificacao, Formato: FormatoTexto.Livre),
        new(SeedId(32), "RG_ORGAO_EMISSOR", "Órgão emissor do RG", "Órgão que emitiu o RG do candidato, entre os de uma lista fechada.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:RG_ORGAO_EMISSOR", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao),
        new(SeedId(56), "RG_ORGAO_EMISSOR_OUTRO", "Nome do órgão emissor do RG", "O órgão emissor do RG que não está na lista.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:RG_ORGAO_EMISSOR_OUTRO", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao, Formato: FormatoTexto.Livre),
        new(SeedId(33), "RG_DATA_EMISSAO", "Data de emissão do RG", "Data em que o RG do candidato foi emitido.",
            DominioFato.Data, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:RG_DATA_EMISSAO", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao),
        new(SeedId(51), "PASSAPORTE_NUMERO", "Número do passaporte", "Número do passaporte do candidato.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:PASSAPORTE_NUMERO", ClassificacaoDeProtecao.Identificador, FinalidadeIdentificacao, Formato: FormatoTexto.Livre),
        new(SeedId(52), "RNM_NUMERO", "Número do Registro Nacional Migratório (RNM)", "Número do Registro Nacional Migratório (RNM) do candidato.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:RNM_NUMERO", ClassificacaoDeProtecao.Identificador, FinalidadeIdentificacao, Formato: FormatoTexto.Livre),
        new(SeedId(36), "NOME_MAE", "Nome da mãe", "Nome da mãe do candidato.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:NOME_MAE", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao, Formato: FormatoTexto.NomePessoa),
        new(SeedId(37), "NOME_PAI", "Nome do pai", "Nome do pai do candidato.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:NOME_PAI", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao, Formato: FormatoTexto.NomePessoa),
        new(SeedId(57), "SEXO_CIVIL", "Sexo civil", "Sexo que consta do registro civil do candidato.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:SEXO_CIVIL", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao),
        new(SeedId(58), "IDENTIDADE_GENERO", "Identidade de gênero", "Identidade de gênero que o candidato declara.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:IDENTIDADE_GENERO", ClassificacaoDeProtecao.Sensivel, FinalidadeIdentificacao),
        new(SeedId(59), "ORIENTACAO_SEXUAL", "Orientação sexual", "Orientação sexual que o candidato declara.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:ORIENTACAO_SEXUAL", ClassificacaoDeProtecao.Sensivel, FinalidadeIdentificacao),
        new(SeedId(38), "ESTADO_CIVIL", "Estado civil", "Estado civil do candidato.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:ESTADO_CIVIL", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao),
        new(SeedId(39), "EMAIL", "E-mail", "Endereço de e-mail para comunicação com o candidato.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:EMAIL", ClassificacaoDeProtecao.Pessoal, FinalidadeComunicacao, Formato: FormatoTexto.Email),
        new(SeedId(40), "TELEFONE", "Telefone", "Telefone, com DDD, para comunicação com o candidato.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:TELEFONE", ClassificacaoDeProtecao.Pessoal, FinalidadeComunicacao, Formato: FormatoTexto.Telefone),
        new(SeedId(41), "RG_UF", "UF de emissão do RG", "Unidade da Federação que emitiu o RG do candidato.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.GeoUf,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:RG_UF", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao),
        new(SeedId(42), "NATURALIDADE_UF", "UF de nascimento", "Unidade da Federação em que o candidato nasceu.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.GeoUf,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:NATURALIDADE_UF", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao),
        new(SeedId(43), "NATURALIDADE_MUNICIPIO", "Município de nascimento", "Município em que o candidato nasceu.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.GeoMunicipio,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:NATURALIDADE_MUNICIPIO", ClassificacaoDeProtecao.Pessoal, FinalidadeIdentificacao),

        new(SeedId(44), "TIPO_ENDERECO", "Tipo de localidade", "Tipo de localidade em que o candidato reside, perguntado antes do endereço.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:TIPO_ENDERECO", ClassificacaoDeProtecao.Pessoal, FinalidadeResidencia),
        new(SeedId(45), "NOME_COMUNIDADE", "Nome da aldeia, comunidade ou quilombo", "Nome da aldeia, da comunidade tradicional ou do quilombo em que o candidato reside.",
            DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:NOME_COMUNIDADE", ClassificacaoDeProtecao.Pessoal, FinalidadeResidencia, Formato: FormatoTexto.Livre),

        new(SeedId(46), OrigemEscolar.FatoFormaDeConclusao, "Forma de conclusão do ensino médio", "Como o candidato concluiu o ensino médio: curso regular, Educação de Jovens e Adultos, ENCCEJA, exame de proficiência ou ENEM.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, $"CAMPO_FORMULARIO:{OrigemEscolar.FatoFormaDeConclusao}", ClassificacaoDeProtecao.Pessoal, FinalidadeReservaDeVagas),
        new(SeedId(47), OrigemEscolar.FatoOndeCursou, "Onde cursou o ensino médio", "Em que rede de ensino o candidato cursou o ensino médio, ou se o concluiu por certificação sem frequentá-lo.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, $"CAMPO_FORMULARIO:{OrigemEscolar.FatoOndeCursou}", ClassificacaoDeProtecao.Pessoal, FinalidadeReservaDeVagas),

    ];
}
