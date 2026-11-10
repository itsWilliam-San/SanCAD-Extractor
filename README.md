<img src=".github/images/Sancad Table.png" alt="Sancad Table"/>

Overview
---
Extraia dados do SanCAD para o programa [Flux](https://github.com/itsWilliam-San/Flux) e dados da Topografia facilmente.

O Programa extrai todos os dados descritos pelos programa Flux com o Plus da RN que vem da planta da Topografia. Para mais informações sobre o que é extraido, veja a documentação do Flux.

Esse programa trabalha via "Bridge", fazendo a ponte entre o CAD e o plugin logo apos esse plugin for carregado via comando `NETLOAD` do ZwCAD. Após inicializado o plugin será listado dentro do programa Flux
e você poderá rodar o comando de extração que transmitirá os dados do CAD para o Flux.

> [!NOTE]
> Esse plugin é **apenas** para extração dos dados, para Exportação, Analise, Visualização ou qualquer outra função, utilize outros plugins dentro do programa Flux que você tenha instalado. Nenhuma outra funcionalidade além da extração de dados é oferecida por esse plugin.

> [!IMPORTANT]
> Este software é um projeto independente, desenvolvido de forma autônoma por um desenvolvedor independente. Ele NÃO é um produto oficial, não possui qualquer vínculo comercial, técnico ou de patrocínio com a Companhia Sanegraph Ltda, nem com qualquer outra entidade corporativa.

How to use
---
Baixe a versão [mais recente](https://github.com/itsWilliam-San/Sewerage-System/releases) feito para o seu CAD (ZwCAD, AutoCAD, ou outro) e carregue o plugin via comando `NETLOAD` ou equivalente da sua plataforma. 
Após carregado, abra o programa Flux e rode o comando de extração de dados. Caso não aparece na lista, atualize a lista de plugins do Flux.

O plugin oferece apenas uma opção de extração sem parâmetros, então apenas acione o botão de ação.

Platform
---
Atualmente testado apenas no ZwCAD, com planos para funcionar no AutoCAD no futuro.

Development Note
---
Este projeto esta sendo feito para atendimento de uma necessidade de uma empresa especifica, então não há planos de desenvolvimento para atender a outros tipos de formatos, software ou necessidades. As únicas alterações que serão feitas no futuro são para correção de bugs, m
elhorias de performance e compatibilidade com novas versões do CAD.

Caso você tenha alguma necessidade especifica que esse projeto não atenda, entre em contato com o desenvolvedor para discutir a possibilidade de implementação.