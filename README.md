# tesseract-train
基于tesseract的训练工具
##简介
采用VS2013编译生成tesseract-train.exe文件，把该文件放到需要识别的图片(.tif)目录中启动
在界面上配置自动识别工具路径[tesseract path]，并指定语言[lang]和字体[font]
训练图片路径[train image path]默认为当前程序运行目录
程序启动后会生成三个临时目录
  [TempBox]   合并图片后保存的文件目录文件名称：{lang}.{font}.exp0.tif
  [TempImg]   对手工识别后保存的图片目录
  [TempTrain] 对标识后图片进行训练生成的中间文件目录，最终生成{lang}.traineddata文件
